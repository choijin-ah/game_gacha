using System;
using System.Globalization;

namespace StarfallAcademy.Lobby
{
    public readonly struct StaminaSnapshot
    {
        public StaminaSnapshot(int current, int maximum, TimeSpan timeUntilNextRecovery)
        {
            Current = current;
            Maximum = maximum;
            TimeUntilNextRecovery = timeUntilNextRecovery;
        }

        public int Current { get; }
        public int Maximum { get; }
        public bool IsFull => Current >= Maximum;
        public TimeSpan TimeUntilNextRecovery { get; }
    }

    // 로컬 UTC 기준 자연 회복을 계산합니다. 서버 연동 시 저장소와 시계만 교체할 수 있습니다.
    public sealed class StaminaService
    {
        const string CurrentKey = "StarfallAcademy.Meta.Stamina.Current";
        const string LastRecoveryUtcTicksKey = "StarfallAcademy.Meta.Stamina.LastRecoveryUtcTicks";
        const string KnownMaximumKey = "StarfallAcademy.Meta.Stamina.KnownMaximum";
        const string PremiumCurrencyKey = "StarfallAcademy.PremiumCurrency";

        public const int BaseMaximumStamina = 120;
        public const int MaximumStaminaPerAccountLevel = 2;
        public static readonly TimeSpan RecoveryInterval = TimeSpan.FromMinutes(6);

        readonly object syncRoot = new object();
        readonly IMetaStorage storage;
        readonly PlayerProfileService profile;
        readonly IUtcClock clock;
        readonly Func<int, int> maximumResolver;

        public static StaminaService Default { get; } = new StaminaService(
            PlayerPrefsMetaStorage.Shared, PlayerProfileService.Default, ContentUtcClock.Shared);

        public StaminaService(IMetaStorage storage, PlayerProfileService profile,
            IUtcClock clock, Func<int, int> maximumResolver = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.maximumResolver = maximumResolver ?? CalculateMaximumStamina;
        }

        public int Current => GetSnapshot().Current;
        public int Maximum => Math.Max(1, maximumResolver(profile.Level));

        public StaminaSnapshot GetSnapshot()
        {
            lock (syncRoot) return Synchronize();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0) return false;
            lock (syncRoot)
            {
                StaminaSnapshot snapshot = Synchronize();
                if (snapshot.Current < amount) return false;
                if (amount == 0) return true;

                DateTime now = NormalizeUtc(clock.UtcNow);
                if (snapshot.IsFull)
                    WriteLastRecoveryUtc(MaxUtc(now, ReadLastRecoveryUtc(now)));
                storage.SetInt(CurrentKey, snapshot.Current - amount);
                storage.Save();
                return true;
            }
        }

        public int Charge(int amount)
        {
            return Charge(amount, false);
        }

        public int Charge(int amount, bool allowOverMaximum)
        {
            if (amount <= 0) return 0;
            lock (syncRoot)
            {
                StaminaSnapshot snapshot = Synchronize();
                int capacity = allowOverMaximum
                    ? int.MaxValue - snapshot.Current
                    : snapshot.Maximum - snapshot.Current;
                int added = Math.Min(amount, Math.Max(0, capacity));
                if (added <= 0) return 0;

                int next = snapshot.Current + added;
                storage.SetInt(CurrentKey, next);
                if (next >= snapshot.Maximum)
                {
                    DateTime now = NormalizeUtc(clock.UtcNow);
                    WriteLastRecoveryUtc(MaxUtc(now, ReadLastRecoveryUtc(now)));
                }
                storage.Save();
                return added;
            }
        }

        public bool TryPurchasePremiumCharge(int premiumCost, int amount,
            bool allowOverMaximum, out int added)
        {
            // The wallet key and stamina key share this storage commit, preventing paid
            // currency from being saved without its matching stamina grant.
            added = 0;
            if (premiumCost < 0 || amount <= 0) return false;
            lock (syncRoot)
            {
                StaminaSnapshot snapshot = Synchronize();
                int capacity = allowOverMaximum
                    ? int.MaxValue - snapshot.Current
                    : snapshot.Maximum - snapshot.Current;
                int purchasable = Math.Min(amount, Math.Max(0, capacity));
                int premiumCurrency = Math.Max(0, storage.GetInt(PremiumCurrencyKey,
                    PlayerWallet.DefaultPremiumCurrency));
                if (purchasable <= 0 || premiumCurrency < premiumCost) return false;

                int next = snapshot.Current + purchasable;
                if (storage is PlayerPrefsMetaStorage)
                {
                    var writes = new MetaIntWrite[]
                    {
                        new MetaIntWrite(PremiumCurrencyKey, premiumCurrency - premiumCost),
                        new MetaIntWrite(CurrentKey, next)
                    };
                    if (!MetaPlayerPrefsTransaction.Commit(writes)) return false;

                    // The paid value pair is already durable. The recovery anchor is
                    // bookkeeping only and can safely be repaired on the next spend.
                    if (next >= snapshot.Maximum)
                    {
                        try
                        {
                            DateTime now = NormalizeUtc(clock.UtcNow);
                            WriteLastRecoveryUtc(MaxUtc(now, ReadLastRecoveryUtc(now)));
                            storage.Save();
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogWarning("[Starfall Meta] Stamina purchase committed, "
                                + "but its recovery anchor will be repaired later: " + exception.Message);
                        }
                    }
                    added = purchasable;
                    return true;
                }

                storage.SetInt(PremiumCurrencyKey, premiumCurrency - premiumCost);
                storage.SetInt(CurrentKey, next);
                if (next >= snapshot.Maximum)
                {
                    DateTime now = NormalizeUtc(clock.UtcNow);
                    WriteLastRecoveryUtc(MaxUtc(now, ReadLastRecoveryUtc(now)));
                }
                storage.Save();
                added = purchasable;
                return true;
            }
        }

        public static int CalculateMaximumStamina(int accountLevel)
        {
            long maximum = BaseMaximumStamina
                + Math.Max(0, accountLevel - 1) * (long)MaximumStaminaPerAccountLevel;
            return maximum >= int.MaxValue ? int.MaxValue : (int)maximum;
        }

        StaminaSnapshot Synchronize()
        {
            DateTime now = NormalizeUtc(clock.UtcNow);
            int maximum = Maximum;
            bool changed = false;

            bool hadCurrent = storage.HasKey(CurrentKey);
            int current = Math.Max(0, storage.GetInt(CurrentKey, maximum));
            if (!hadCurrent || storage.GetInt(CurrentKey, maximum) != current)
            {
                storage.SetInt(CurrentKey, current);
                changed = true;
            }

            int knownMaximum = Math.Max(1, storage.GetInt(KnownMaximumKey, maximum));
            DateTime lastRecoveryUtc = ReadLastRecoveryUtc(now);
            if (!storage.HasKey(LastRecoveryUtcTicksKey))
            {
                WriteLastRecoveryUtc(now);
                lastRecoveryUtc = now;
                changed = true;
            }

            if (knownMaximum != maximum)
            {
                // 이전 최대치에서 가득 찬 시간은 새 최대치의 회복분으로 소급하지 않습니다.
                if (current >= knownMaximum)
                {
                    lastRecoveryUtc = MaxUtc(lastRecoveryUtc, now);
                    WriteLastRecoveryUtc(lastRecoveryUtc);
                }
                storage.SetInt(KnownMaximumKey, maximum);
                changed = true;
            }
            else if (!storage.HasKey(KnownMaximumKey))
            {
                storage.SetInt(KnownMaximumKey, maximum);
                changed = true;
            }

            if (current < maximum)
            {
                long elapsedTicks = Math.Max(0L, (now - lastRecoveryUtc).Ticks);
                long recovered = elapsedTicks / RecoveryInterval.Ticks;
                if (recovered > 0)
                {
                    int actualRecovery = (int)Math.Min(recovered, maximum - current);
                    current += actualRecovery;
                    storage.SetInt(CurrentKey, current);

                    DateTime nextAnchor = current >= maximum
                        ? now
                        : lastRecoveryUtc.AddTicks(recovered * RecoveryInterval.Ticks);
                    WriteLastRecoveryUtc(nextAnchor);
                    lastRecoveryUtc = nextAnchor;
                    changed = true;
                }
            }

            if (changed) storage.Save();
            TimeSpan untilNext;
            if (current >= maximum)
                untilNext = TimeSpan.Zero;
            else if (lastRecoveryUtc > now)
                untilNext = (lastRecoveryUtc - now) + RecoveryInterval;
            else
                untilNext = RecoveryInterval - TimeSpan.FromTicks(
                    (now - lastRecoveryUtc).Ticks % RecoveryInterval.Ticks);
            return new StaminaSnapshot(current, maximum, untilNext);
        }

        DateTime ReadLastRecoveryUtc(DateTime fallback)
        {
            string raw = storage.GetString(LastRecoveryUtcTicksKey, string.Empty);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                return fallback;
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return fallback;
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        void WriteLastRecoveryUtc(DateTime utc)
        {
            storage.SetString(LastRecoveryUtcTicksKey,
                utc.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        static DateTime MaxUtc(DateTime first, DateTime second) =>
            first >= second ? first : second;

        static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}

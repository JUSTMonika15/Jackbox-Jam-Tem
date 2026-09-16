using System;
using System.Collections.Generic;

public sealed class RaceRules
{
    public long Score(int laps, int money) => (long)Math.Max(0, laps) * 100 + Math.Max(0, money);
}

public sealed class PurchaseHoldRules
{
    object target;
    float elapsed;
    bool interrupted;
    public float Progress => Math.Min(1f, elapsed / 2f);
    public bool Ready => target != null && elapsed >= 2f && !interrupted;
    public bool IsReadyFor(object land) => Ready && ReferenceEquals(target, land);
    public bool Tick(object land, bool held, bool eligible, float delta)
    {
        if (!held) { Reset(); return false; }
        if (interrupted) return false;
        if (!eligible || land == null) { target = null; elapsed = 0f; return false; }
        if (!ReferenceEquals(target, land)) { target = land; elapsed = 0f; }
        if (!float.IsNaN(delta) && !float.IsInfinity(delta) && delta > 0f)
            elapsed = Math.Min(2f, elapsed + delta);
        return Ready;
    }
    public void Interrupt() { target = null; elapsed = 0f; interrupted = true; }
    public void Cancel() { target = null; elapsed = 0f; }
    public void Reset() { target = null; elapsed = 0f; interrupted = false; }
}

public sealed class JumpRules
{
    public float InitialVelocity(float height, float gravity)
        => height > 0f && gravity < 0f ? (float)Math.Sqrt(-2f * gravity * height) : 0f;
}

// One visit can have several body colliders; only a complete exit permits a redraw.
public sealed class ZoneVisit
{
    readonly HashSet<object> colliders = new HashSet<object>();
    public bool Enter(object collider)
    {
        bool first = colliders.Count == 0;
        colliders.Add(collider);
        return first;
    }
    public void Exit(object collider) { colliders.Remove(collider); }
    public bool IsEmpty => colliders.Count == 0;
}

public sealed class PlayerEffectRules
{
    float jailedUntil = float.NegativeInfinity;
    float immuneUntil = float.NegativeInfinity;
    float speedUntil = float.NegativeInfinity;
    float speedMultiplier = 1f;
    public bool HasJailPass { get; private set; }
    public bool HasTollPass { get; private set; }
    public bool IsJailed(float now) => now < jailedUntil;
    public float JailRemaining(float now) => Math.Max(0f, jailedUntil - now);
    public void GrantJailPass() { HasJailPass = true; }
    public void GrantTollPass() { HasTollPass = true; }
    public bool ConsumeTollPass()
    {
        if (!HasTollPass) return false;
        HasTollPass = false;
        return true;
    }
    public bool TryJail(float now, float seconds)
    {
        if (float.IsNaN(now) || float.IsInfinity(now) || seconds <= 0f
            || float.IsNaN(seconds) || float.IsInfinity(seconds) || now < immuneUntil || IsJailed(now)) return false;
        if (HasJailPass)
        {
            HasJailPass = false;
            immuneUntil = now + 2f;
            return false;
        }
        jailedUntil = now + seconds;
        immuneUntil = jailedUntil + 2f;
        return true;
    }
    public void SetSpeed(float multiplier, float seconds, float now)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f
            || float.IsNaN(now) || float.IsInfinity(now) || seconds <= 0f
            || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        speedMultiplier = multiplier;
        speedUntil = now + seconds;
    }
    public float Speed(float now) => now < speedUntil ? speedMultiplier : 1f;
    public void Reset()
    {
        jailedUntil = immuneUntil = speedUntil = float.NegativeInfinity;
        speedMultiplier = 1f;
        HasJailPass = HasTollPass = false;
    }
}

public enum BoardEventKind { Bonus, Bill, Birthday, Dividend, JailPass, ReturnStart, Jail, SpeedUp, SlowDown, TollPass }
public sealed class BoardEventRules
{
    public BoardEventKind Resolve(bool fortune, int index)
    {
        if (index < 0 || index >= 5) throw new ArgumentOutOfRangeException(nameof(index));
        return (BoardEventKind)((fortune ? 0 : 5) + index);
    }
    public BoardEventKind ResolveWeighted(bool fortune,int roll)
    {
        if (roll < 0 || roll >= 20) throw new ArgumentOutOfRangeException(nameof(roll));
        if (fortune) return Resolve(true,roll / 4);
        if (roll < 2) return BoardEventKind.ReturnStart;
        if (roll < 3) return BoardEventKind.Jail;
        if (roll < 10) return BoardEventKind.SpeedUp;
        if (roll < 14) return BoardEventKind.SlowDown;
        return BoardEventKind.TollPass;
    }
    public int ApplyMoney(BoardEventKind kind, EconomyAccount wallet, IEnumerable<EconomyAccount> peers, int properties)
    {
        if (wallet == null) return 0;
        int before = wallet.Money;
        if (kind == BoardEventKind.Bonus) wallet.AddMoney(30);
        else if (kind == BoardEventKind.Bill) wallet.AddMoney(-20);
        else if (kind == BoardEventKind.Dividend)
            wallet.AddMoney((int)Math.Min(int.MaxValue, (long)Math.Max(0, properties) * 5));
        else if (kind == BoardEventKind.Birthday && peers != null)
        {
            var visited = new HashSet<EconomyAccount>();
            foreach (EconomyAccount peer in peers)
            {
                if (peer == null || ReferenceEquals(peer, wallet) || !visited.Add(peer)) continue;
                int paid = Math.Min(5, Math.Min(peer.Money, int.MaxValue - wallet.Money));
                peer.TrySpend(paid);
                wallet.AddMoney(paid);
            }
        }
        return wallet.Money - before;
    }
}

// Deterministic rules, independent of Unity and the existing networking template.
public sealed class EconomyAccount
{
    public int Money { get; private set; }
    public EconomyAccount(int startingMoney) { Money = Math.Max(0, startingMoney); }
    public void SetMoney(int amount) { Money = Math.Max(0, amount); }
    public void AddMoney(int amount)
    {
        Money = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)Money + amount));
    }
    public bool TrySpend(int amount)
    {
        if (amount < 0 || Money < amount) return false;
        Money -= amount;
        return true;
    }
}

public sealed class PropertyRules
{
    public int Price { get; }
    public int Toll { get; }
    public EconomyAccount Owner { get; private set; }
    public PropertyRules(int price, int toll)
    {
        if (price < 0 || toll < 0) throw new ArgumentOutOfRangeException();
        Price = price;
        Toll = toll;
    }
    public bool TryBuy(EconomyAccount buyer)
    {
        if (buyer == null || Owner != null || !buyer.TrySpend(Price)) return false;
        Owner = buyer;
        return true;
    }
    public int CollectRent(EconomyAccount visitor)
    {
        if (Owner == null || visitor == null || ReferenceEquals(Owner, visitor)) return 0;
        int paid = Math.Min(visitor.Money, Toll);
        visitor.TrySpend(paid);
        Owner.AddMoney(paid);
        return paid;
    }
    public bool SellToBank(EconomyAccount seller)
    {
        if (seller == null || !ReferenceEquals(Owner, seller)) return false;
        seller.AddMoney(Price / 2);
        Owner = null;
        return true;
    }
}

public sealed class ForcedPaymentResult
{
    public int Paid { get; }
    public bool NeedsWork { get; }
    public ForcedPaymentResult(int paid, bool needsWork) { Paid = paid; NeedsWork = needsWork; }
}

// Mandatory bills only: voluntary purchases must never liquidate property.
public sealed class ForcedPaymentRules
{
    public ForcedPaymentResult Settle(EconomyAccount payer, int amount,
        IEnumerable<PropertyRules> properties, EconomyAccount recipient)
    {
        if (payer == null || amount < 0) throw new ArgumentOutOfRangeException();
        if (ReferenceEquals(payer, recipient)) return new ForcedPaymentResult(0, false);
        // A full recipient wallet cannot accept more money; do not sell land unnecessarily.
        int requested = recipient == null ? amount : Math.Min(amount, int.MaxValue - recipient.Money);
        if (payer.Money < requested && properties != null)
        {
            var owned = new List<PropertyRules>();
            var seen = new HashSet<PropertyRules>();
            foreach (PropertyRules land in properties)
                if (land != null && ReferenceEquals(land.Owner, payer) && seen.Add(land)) owned.Add(land);
            owned.Sort((a,b) => a.Price.CompareTo(b.Price));
            foreach (PropertyRules land in owned)
            {
                if (payer.Money >= requested) break;
                land.SellToBank(payer);
            }
        }
        int paid = Math.Min(payer.Money, requested);
        payer.TrySpend(paid);
        if (recipient != null) recipient.AddMoney(paid);
        return new ForcedPaymentResult(paid, paid < requested);
    }
}

public sealed class WorkRules
{
    bool pendingWage;
    float until;
    // Keep actions blocked until completion is processed, even at the timer boundary.
    public bool IsWorking(float now) => pendingWage;
    public float Remaining(float now) => pendingWage ? Math.Max(0f, until - now) : 0f;
    public bool Start(float now)
    {
        if (pendingWage || float.IsNaN(now) || float.IsInfinity(now)) return false;
        pendingWage = true; until = now + 3f; return true;
    }
    public bool Complete(float now)
    {
        if (!pendingWage || float.IsNaN(now) || now < until) return false;
        pendingWage = false; return true;
    }
    public void Reset() { pendingWage = false; until = 0f; }
}

public enum GameState { Waiting, Countdown, Playing, Results }

public enum BoardSpaceKind
{
    Start, Property, Fortune, Chance, IncomeTax, LuxuryTax,
    MovementBonus, SlowStrip, JailVisit, SpeedStrip, GoToJail
}

public sealed class BoardSpaceDefinition
{
    public BoardSpaceKind Kind { get; }
    public string Name { get; }
    public int Price { get; }
    public int Toll => Math.Max(0, Price / 10);
    public BoardSpaceDefinition(BoardSpaceKind kind, string name, int price = 0)
    {
        Kind = kind;
        Name = name ?? string.Empty;
        Price = Math.Max(0, price);
    }
}

public readonly struct PropertyCardPresentation
{
    public string Title { get; }
    public string PriceText { get; }
    public string RentText { get; }
    public string OwnerText { get; }
    public string ActionText { get; }

    public PropertyCardPresentation(string title, int price, int rent, string ownerName, int playerMoney, bool canAct)
    {
        int safePrice = Math.Max(0, price);
        Title = string.IsNullOrWhiteSpace(title) ? "PROPERTY" : title;
        PriceText = "PRICE  $" + safePrice;
        RentText = "RENT  $" + Math.Max(0, rent);
        if (!string.IsNullOrWhiteSpace(ownerName))
        {
            OwnerText = "OWNER: " + ownerName;
            ActionText = "PAY RENT WHEN PASSING";
        }
        else
        {
            OwnerText = "UNOWNED PROPERTY";
            ActionText = !canAct ? "CANNOT ACT NOW"
                : playerMoney < safePrice ? "NEED $" + (safePrice - Math.Max(0, playerMoney)) + " MORE"
                : "HOLD E FOR 2 SECONDS TO BUY";
        }
    }
}

public sealed class ToyBoardPalette
{
    static readonly string[] PropertyRoadColors = { "#FF6B6B", "#FFB84D", "#48C9B0", "#9B7EDE" };

    public string RimHex => "#FFF2C7";
    public string CenterHex => "#6ED6E8";
    public string CenterAccentHex => "#FFF0A8";
    public string BoundaryHex => "#3A315A";
    public string UnownedPropertyColor(int boardIndex) => ColorFor(boardIndex,BoardSpaceKind.Property);
    public string ColorFor(int boardIndex, BoardSpaceKind kind)
    {
        if (kind == BoardSpaceKind.Property)
            return PropertyRoadColors[Math.Max(0, boardIndex / 10) % PropertyRoadColors.Length];
        switch (kind)
        {
            case BoardSpaceKind.Fortune: return "#37C978";
            case BoardSpaceKind.Chance: return "#4D8DFF";
            case BoardSpaceKind.IncomeTax: return "#E94B5F";
            case BoardSpaceKind.LuxuryTax: return "#E45AA8";
            case BoardSpaceKind.MovementBonus: return "#FFD166";
            case BoardSpaceKind.SlowStrip: return "#7B61D1";
            case BoardSpaceKind.SpeedStrip: return "#2ED8D0";
            case BoardSpaceKind.GoToJail: return "#F04444";
            case BoardSpaceKind.JailVisit: return "#6C7180";
            case BoardSpaceKind.Start: return "#FFCF4A";
            default: return "#F2C94C";
        }
    }
}

// Shared presentation numbers keep the runtime HUD/camera consistent and let the
// jam's important screen proportions be checked without loading a Unity scene.
public sealed class BoardPresentationRules
{
    public float CameraViewportBottom => 0f;
    public float CameraViewportHeight => 1f;
    public float CameraHalfHeight => 12.5f;
    public float PropertyCardWidth => 300f;
    public float PropertyCardHeight => 154f;
    public float EventCardWidth => 340f;
    public float EventCardHeight => 120f;
    public float PropertyCardTop(float screenHeight)
        => Math.Max(0f,(screenHeight - PropertyCardHeight) * .5f);
    public float EventCardTop(float screenHeight)
        => Math.Max(0f,(screenHeight - EventCardHeight) * .5f);
    public float SidePanelWidth(float screenWidth,float screenHeight)
        => Math.Max(220f,Math.Min(270f,(screenWidth-screenHeight)*.5f-12f));

    public string TileLabel(BoardSpaceDefinition definition)
    {
        if (definition == null) return string.Empty;
        return definition.Kind == BoardSpaceKind.Property
            ? definition.Name + "\n$" + definition.Price + " / RENT $" + definition.Toll
            : definition.Name;
    }

    public float TileLabelAngle(int boardIndex)
    {
        switch (Math.Max(0, boardIndex) / 10 % 4)
        {
            case 0: return 90f;
            case 1: return 0f;
            case 2: return -90f;
            default: return 0f;
        }
    }
}

public sealed class ContinuousBoardRules
{
    static readonly string[][] CityNames =
    {
        new[] { "Guangzhou", "Shenzhen", "Xiamen", "Hangzhou", "Suzhou", "Nanjing", "Shanghai" },
        new[] { "Seoul", "Tokyo", "Singapore", "Bangkok", "Sydney", "Dubai", "Istanbul" },
        new[] { "Rome", "Paris", "Berlin", "London", "Toronto", "New York", "Los Angeles" },
        new[] { "Rio", "Cape Town", "Barcelona", "Vienna", "Copenhagen", "Oslo", "Reykjavik" }
    };

    public IReadOnlyList<BoardSpaceDefinition> CreateSpaces()
    {
        var spaces = new List<BoardSpaceDefinition>(40)
        {
            new BoardSpaceDefinition(BoardSpaceKind.Start, "START")
        };
        AddSide(spaces, 0, 20, BoardSpaceKind.Fortune, "FORTUNE", BoardSpaceKind.IncomeTax, "INCOME TAX");
        spaces.Add(new BoardSpaceDefinition(BoardSpaceKind.JailVisit, "JAIL / VISIT"));
        AddSide(spaces, 1, 30, BoardSpaceKind.Chance, "CHANCE", BoardSpaceKind.MovementBonus, "MOVE BONUS");
        spaces.Add(new BoardSpaceDefinition(BoardSpaceKind.SpeedStrip, "SPEED BOOST"));
        AddSide(spaces, 2, 40, BoardSpaceKind.Fortune, "FORTUNE", BoardSpaceKind.LuxuryTax, "LUXURY TAX");
        spaces.Add(new BoardSpaceDefinition(BoardSpaceKind.GoToJail, "GO TO JAIL"));
        AddSide(spaces, 3, 50, BoardSpaceKind.Chance, "CHANCE", BoardSpaceKind.SlowStrip, "SLOWDOWN");
        return spaces;
    }

    static void AddSide(List<BoardSpaceDefinition> spaces, int side, int startingPrice,
        BoardSpaceKind firstSpecial, string firstName, BoardSpaceKind secondSpecial, string secondName)
    {
        int city = 0;
        for (int slot = 1; slot <= 9; slot++)
        {
            if (slot == 3) spaces.Add(new BoardSpaceDefinition(firstSpecial, firstName));
            else if (slot == 5) spaces.Add(new BoardSpaceDefinition(secondSpecial, secondName));
            else
            {
                int price = startingPrice + city * 10;
                spaces.Add(new BoardSpaceDefinition(BoardSpaceKind.Property, CityNames[side][city], price));
                city++;
            }
        }
    }
}

public sealed class PushCooldown
{
    readonly float duration;
    float readyAt = float.NegativeInfinity;
    public PushCooldown(float seconds) { duration = Math.Max(0f, seconds); }
    public float Remaining(float now) => Math.Max(0f, readyAt - now);
    public bool TryUse(bool canPlay, float now)
    {
        if (!canPlay || float.IsNaN(now) || float.IsInfinity(now) || now < readyAt) return false;
        readyAt = now + duration;
        return true;
    }
    public void Reset() { readyAt = float.NegativeInfinity; }
}

public sealed class MatchClock
{
    readonly float countdownDuration;
    readonly float matchDuration;
    public GameState State { get; private set; } = GameState.Waiting;
    public float CountdownRemaining { get; private set; }
    public float Remaining { get; private set; }
    public MatchClock(float countdown, float duration)
    {
        countdownDuration = Math.Max(0f, countdown);
        matchDuration = Math.Max(0.01f, duration);
        Remaining = matchDuration;
    }
    public void Begin()
    {
        Remaining = matchDuration;
        CountdownRemaining = countdownDuration;
        State = countdownDuration > 0f ? GameState.Countdown : GameState.Playing;
    }
    public void ApplyNetwork(GameState state, float remaining, float countdownRemaining)
    {
        if (!Enum.IsDefined(typeof(GameState), state)) return;
        if (float.IsNaN(remaining) || float.IsInfinity(remaining)) remaining = 0f;
        if (float.IsNaN(countdownRemaining) || float.IsInfinity(countdownRemaining)) countdownRemaining = 0f;
        State = state;
        Remaining = Math.Max(0f, Math.Min(matchDuration, remaining));
        CountdownRemaining = Math.Max(0f, Math.Min(countdownDuration, countdownRemaining));
    }
    public void Tick(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
        if (State == GameState.Countdown)
        {
            float consumed = Math.Min(CountdownRemaining, deltaTime);
            CountdownRemaining -= consumed;
            deltaTime -= consumed;
            if (CountdownRemaining <= 0f) State = GameState.Playing;
        }
        if (State != GameState.Playing) return;
        Remaining = Math.Max(0f, Remaining - deltaTime);
        if (Remaining <= 0f) State = GameState.Results;
    }
}

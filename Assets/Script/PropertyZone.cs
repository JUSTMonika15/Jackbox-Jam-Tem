using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class PropertyZone : MonoBehaviour
{
    [SerializeField] private string propertyName = "Property";
    [SerializeField, Min(0)] private int price = 20;
    [SerializeField, Min(0)] private int toll = 5;
    [SerializeField] private Renderer zoneRenderer;
    [SerializeField] private TextMesh zoneLabel;
    [SerializeField] private Color neutralColor = new Color(0.6f, 0.6f, 0.6f);
    [SerializeField, Min(0f)] private float networkRequestTolerance = 1.5f;
    private readonly HashSet<PlayerState> localVisitors = new HashSet<PlayerState>();
    private readonly HashSet<PlayerState> authoritativeVisitors = new HashSet<PlayerState>();
    private PropertyRules rules;
    private MaterialPropertyBlock colorBlock;
    private GameManager game;
    private BoxCollider trigger;
    public string PropertyName => propertyName;
    public int Price => price;
    public int Toll => toll;
    public PlayerState Owner { get; private set; }
    internal PropertyRules Rules => rules;

    private void Awake()
    {
        trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        if (zoneRenderer == null) zoneRenderer = GetComponentInChildren<Renderer>();
        game = FindAnyObjectByType<GameManager>();
        colorBlock = new MaterialPropertyBlock();
        ResetProperty();
    }

    private void Update()
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (game == null || trigger == null) return;

        UpdateLocalContact(game.GetLocalPlayer());
        if (!game.IsAuthoritative) return;

        foreach (PlayerState player in game.Players)
        {
            if (player == null) continue;
            bool standing = player.IsStandingOn(trigger);
            if (!standing)
            {
                authoritativeVisitors.Remove(player);
                continue;
            }
            if (authoritativeVisitors.Add(player) && player.CanAct) ChargeRent(player);
        }
        authoritativeVisitors.RemoveWhere(player => player == null || !player.isActiveAndEnabled);
    }

    private void UpdateLocalContact(PlayerState player)
    {
        bool standing = player != null && player.CanAct && player.IsStandingOn(trigger);
        if (standing)
        {
            if (localVisitors.Add(player)) player.EnterProperty(this);
            return;
        }
        if (player != null && localVisitors.Remove(player)) player.ExitProperty(this);
        localVisitors.RemoveWhere(visitor => visitor == null || !visitor.isActiveAndEnabled);
    }

    private void ChargeRent(PlayerState player)
    {
        if (player == null || !player.CanAct) return;
        if (Owner == null || Owner == player) return;
        if (player.ConsumeTollPass())
        {
            player.ShowFeedback("TOLL PASS USED\nNo rent paid", PlayerFeedbackKind.Pass);
            return;
        }
        PlayerState landlord = Owner;
        int paid = player.PayMandatory(toll, landlord);
        if (paid > 0)
        {
            landlord.RecordRent(paid);
            player.ShowFeedback("RENT PAID\n-$" + paid + " to " + landlord.DisplayName,
                PlayerFeedbackKind.Negative);
        }
    }
    private void OnDisable()
    {
        foreach (PlayerState player in localVisitors)
            if (player != null) player.ExitProperty(this);
        localVisitors.Clear();
        authoritativeVisitors.Clear();
    }
    public bool TryBuy(PlayerState player)
        => TryBuyInternal(player, false);

    internal bool TryBuyFromNetwork(PlayerState player)
        => TryBuyInternal(player, true);

    private bool TryBuyInternal(PlayerState player, bool networkHoldConfirmed)
    {
        if (game == null) game = FindAnyObjectByType<GameManager>();
        if (game != null && !game.IsAuthoritative) return false;
        bool holdReady = networkHoldConfirmed || (player != null && player.IsPurchaseReadyFor(this));
        bool inZone = player != null && (localVisitors.Contains(player)
            || (networkHoldConfirmed && IsWithinNetworkRange(player)));
        if (player == null || !RequestValidation.CanBuy(game != null && game.CanPlay, player.CanAct,
            inZone, Owner == null, holdReady, player.Money, price)) return false;
        if (Owner != null)
        {
            player.ShowMessage(Owner == player ? "You already own this property" : "Owned by " + Owner.DisplayName);
            return false;
        }
        if (!rules.TryBuy(player.Account))
        {
            player.ShowMessage("Need $" + price + " to buy " + propertyName);
            return false;
        }
        Owner = player;
        player.RecordPurchase();
        player.ShowFeedback("PROPERTY BOUGHT\n" + propertyName + " for $" + price,
            PlayerFeedbackKind.Positive);
        SetPresentation(player.PlayerColor, true);
        if (game != null) game.BroadcastPropertyState(this);
        return true;
    }
    private bool IsWithinNetworkRange(PlayerState player)
    {
        if (player == null || trigger == null) return false;
        return player.IsStandingOn(trigger, Mathf.Min(.35f, networkRequestTolerance));
    }
    public void ResetProperty()
    {
        foreach (PlayerState player in localVisitors)
            if (player != null) player.ExitProperty(this);
        localVisitors.Clear();
        authoritativeVisitors.Clear();
        Owner = null;
        rules = new PropertyRules(Mathf.Max(0, price), Mathf.Max(0, toll));
        SetPresentation(neutralColor, false);
        if (game != null && game.IsAuthoritative) game.BroadcastPropertyState(this);
    }
    public void ForgetVisitor(PlayerState player)
    {
        localVisitors.Remove(player);
        authoritativeVisitors.Remove(player);
        if (player != null) player.ExitProperty(this);
    }
    internal bool RefreshBankSale()
    {
        if (Owner == null || rules.Owner != null) return false;
        Owner = null;
        SetPresentation(neutralColor, false);
        if (game != null) game.BroadcastPropertyState(this);
        // Keep contact records: standing inside a sold tile must not retrigger rent.
        return true;
    }
    internal void ApplyNetworkOwner(PlayerState owner, Color ownerColor)
    {
        Owner = owner;
        SetPresentation(owner == null ? neutralColor : ownerColor, owner != null);
    }
    public void Configure(string displayName,int purchasePrice,int rent,Renderer renderer,TextMesh label,Color unownedColor)
    {
        propertyName = string.IsNullOrWhiteSpace(displayName) ? "Property" : displayName;
        price = Mathf.Max(0, purchasePrice);
        toll = Mathf.Max(0, rent);
        zoneRenderer = renderer;
        zoneLabel = label;
        neutralColor = unownedColor;
        ResetProperty();
    }
    private void SetPresentation(Color color, bool owned)
    {
        SetColor(color);
        if (zoneLabel == null) return;
        var palette = new ToyBoardPalette();
        zoneLabel.color = ColorUtility.TryParseHtmlString(palette.PropertyLabelHex(owned), out Color labelColor)
            ? labelColor : (owned ? Color.black : Color.white);
    }
    private void SetColor(Color color)
    {
        if (zoneRenderer == null || colorBlock == null) return;
        zoneRenderer.GetPropertyBlock(colorBlock);
        colorBlock.SetColor("_BaseColor", color);
        colorBlock.SetColor("_Color", color);
        zoneRenderer.SetPropertyBlock(colorBlock);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Owner != null ? Owner.PlayerColor : neutralColor;
        BoxCollider box = GetComponent<BoxCollider>();
        Gizmos.matrix = transform.localToWorldMatrix;
        if (box != null) Gizmos.DrawWireCube(box.center, box.size);
    }
}

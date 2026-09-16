using UnityEngine;
using System.Collections.Generic;

// Screen-space prototype HUD; the lobby UI is unchanged.
[RequireComponent(typeof(GameManager))]
public class GameHud : MonoBehaviour
{
    GameManager game;
    readonly BoardPresentationRules presentation = new BoardPresentationRules();
    GUIStyle text, centered, arrow, cardTitle, cardValue, cardStatus;
    readonly List<PlayerState> standings = new List<PlayerState>();
    public bool SelfArrowVisible => game != null && game.GetLocalPlayer() != null
        && (game.CurrentState == GameState.Waiting || game.CurrentState == GameState.Countdown);
    void Awake() { game = GetComponent<GameManager>(); }
    void OnEnable() { text = null; centered = null; arrow = null; cardTitle = null; cardValue = null; cardStatus = null; }
    void OnGUI()
    {
        if (game == null) return;
        // Hot reload can retain the previous text style without the newly added styles.
        if (text == null || centered == null || arrow == null || cardTitle == null || cardValue == null || cardStatus == null)
        {
            text = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
            arrow = new GUIStyle(centered) { fontSize = 26, fontStyle = FontStyle.Bold };
            cardTitle = new GUIStyle(centered) { fontSize = 22, fontStyle = FontStyle.Bold };
            cardValue = new GUIStyle(centered) { fontSize = 18, fontStyle = FontStyle.Bold };
            cardStatus = new GUIStyle(centered) { fontSize = 15, wordWrap = true };
            text.normal.textColor = Color.white;
            centered.normal.textColor = Color.white;
            arrow.normal.textColor = Color.white;
            cardTitle.normal.textColor = Color.white;
            cardValue.normal.textColor = Color.white;
            cardStatus.normal.textColor = Color.white;
        }
        float scale = Mathf.Max(.5f,Mathf.Min(Screen.width/1280f,Screen.height/720f));
        float w = Screen.width/scale, h = Screen.height/scale;
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1f));
        PlayerState player = game.GetLocalPlayer();
        standings.Clear();
        foreach (PlayerState entry in game.Players) if (entry != null) standings.Add(entry);
        standings.Sort((a,b) => {
            int score = b.Score.CompareTo(a.Score);
            return score != 0 ? score : string.CompareOrdinal(a.DisplayName,b.DisplayName);
        });
        float sideWidth = presentation.SidePanelWidth(w,h);
        Panel(new Rect(12,68,sideWidth,70));
        if (player != null)
        {
            GUI.color = player.PlayerColor;
            GUI.DrawTexture(new Rect(24,84,12,38),Texture2D.whiteTexture);
            GUI.color = oldColor;
            Label(new Rect(46,76,sideWidth-46,28),player.DisplayName+"  $"+player.Money);
            Label(new Rect(46,104,sideWidth-46,28),"Laps "+player.LapCount+"   Score "+player.Score);
        }
        Panel(new Rect(w/2-105,8,210,42));
        GUI.Label(new Rect(w/2-101,10,202,38),game.CurrentState+"  "+Mathf.CeilToInt(game.TimeRemaining)+"s",centered);
        GUI.Label(new Rect(w-94,10,82,24),game.NetworkVersionLabel,centered);
        Panel(new Rect(12,146,sideWidth,70));
        Label(new Rect(24,153,sideWidth-24,28),"WASD move | Space jump");
        Label(new Rect(24,181,sideWidth-24,28),"Hold E buy | Click push: "+(player == null || player.PushCooldownRemaining <= 0f
            ? "Ready" : player.PushCooldownRemaining.ToString("0.0")+"s"));
        if (game.HasProtocolWarning)
        {
            GUI.color = new Color(1f,.2f,.15f);
            GUI.Label(new Rect(w/2-280,67,560,52),
                "NETWORK BUILD MISMATCH — rebuild and relaunch both players",centered);
            GUI.color = oldColor;
        }
        else if (game.IsNetworked && !game.IsAuthoritative && game.NetworkSnapshotAge > 2f)
        {
            GUI.color = new Color(1f,.35f,.25f);
            GUI.Label(new Rect(w/2-210,67,420,28),"Synchronizing with host...",centered);
            GUI.color = oldColor;
        }
        if (game.CurrentState != GameState.Results)
        {
            float x = w-sideWidth-12;
            Panel(new Rect(x,68,sideWidth,82+standings.Count*62));
            Label(new Rect(x+12,76,sideWidth-24,28),"LIVE RANKING");
            Label(new Rect(x+12,106,sideWidth-24,28),"Score = Laps x 100 + Cash");
            for (int i=0;i<standings.Count;i++)
            {
                PlayerState entry = standings[i];
                float y = 142+i*62;
                GUI.color = entry.PlayerColor;
                GUI.DrawTexture(new Rect(x+12,y+3,8,42),Texture2D.whiteTexture);
                GUI.color = oldColor;
                Label(new Rect(x+26,y,sideWidth-38,27),(i+1)+". "+entry.DisplayName+(entry==player?" (YOU)":""));
                Label(new Rect(x+26,y+27,sideWidth-38,27),"Score "+entry.Score+" | Lap "+entry.LapCount+" | $"+entry.Money);
            }
        }
        if (player != null)
        {
            Panel(new Rect(12,h-96,sideWidth,84));
            PlayerEffects effects = player.GetComponent<PlayerEffects>();
            if (effects != null)
            {
                string state = effects.IsJailed ? "Jailed "+effects.JailRemaining.ToString("0.0")+"s"
                    : effects.IsWorking ? "Working "+effects.WorkRemaining.ToString("0.0")+"s (+$10)"
                    : "Speed x"+effects.SpeedMultiplier.ToString("0.0");
                Label(new Rect(24,h-88,sideWidth-24,28),state);
                Label(new Rect(24,h-58,sideWidth-24,28),"Jail pass "+(effects.HasJailPass?1:0)+" | Toll pass "+(effects.HasTollPass?1:0));
            }
        }
        if (game.CurrentState == GameState.Waiting)
        {
            string waitingText = player == null ? "Waiting for your player..."
                : game.IsNetworked ? "Players ready - host will start the countdown..."
                : "Find your YOU arrow, then start!";
            GUI.Label(new Rect(w/2-250,h/2+90,500,32),waitingText,centered);
            if (!game.IsNetworked)
            {
                GUI.enabled = player != null;
                if (GUI.Button(new Rect(w/2-110,h/2+126,220,32),"Start match")) game.StartMatch();
                GUI.enabled = true;
            }
        }
        else if (game.CurrentState == GameState.Countdown)
            GUI.Label(new Rect(w/2-160,h/2+80,320,60),"Ready in "+Mathf.CeilToInt(game.CountdownRemaining),arrow);
        else if (game.CurrentState == GameState.Playing && player != null)
        {
            PropertyZone land = player.IsGrounded ? player.NearbyProperty : null;
            if (land != null)
                DrawPropertyCard(w,h,player,land,oldColor);
            else if (!string.IsNullOrEmpty(player.Message))
                DrawEventCard(w,h,player.Message,oldColor);
        }
        else if (game.CurrentState == GameState.Results)
        {
            Panel(new Rect(w/2-320,h/2-185,640,360));
            string winners = "";
            foreach (PlayerState result in game.Results)
            {
                if (result == null) continue;
                if (game.Results.Count > 0 && result.Score != game.Results[0].Score) break;
                winners += (winners.Length==0?"":" / ")+result.DisplayName;
            }
            GUI.Label(new Rect(w/2-300,h/2-175,600,60),winners.Length==0 ? "RESULTS" : "WINNER: "+winners,arrow);
            GUI.Label(new Rect(w/2-300,h/2-113,600,26),"Score = Laps x 100 + Cash (equal top scores share victory)",centered);
            int row = 0;
            foreach (PlayerState result in game.Results)
            {
                if (result == null) continue;
                Label(new Rect(w/2-295,h/2-75+row*42,590,38),(row+1)+". "+result.DisplayName
                    +"  Score "+result.Score+"  Laps "+result.LapCount+"  $"+result.Money);
                row++;
            }
            if (game.CanControlMatch && GUI.Button(new Rect(w/2-110,h/2+110,220,32),"Play again")) game.StartMatch();
            else if (!game.CanControlMatch)
                GUI.Label(new Rect(w/2-180,h/2+110,360,32),"Waiting for host...",centered);
        }
        if (SelfArrowVisible && Camera.main != null)
        {
            Vector3 point = Camera.main.WorldToScreenPoint(player.transform.position+Vector3.up*1.4f);
            if (point.z > 0f)
            {
                float y = (Screen.height-point.y)/scale-48-Mathf.Sin(Time.unscaledTime*3f)*8f;
                GUI.color = Color.black;
                GUI.Label(new Rect(point.x/scale-79,y+2,160,42),"YOU ▼",arrow);
                GUI.color = player.PlayerColor;
                GUI.Label(new Rect(point.x/scale-80,y,160,42),"YOU ▼",arrow);
            }
        }
        GUI.color = oldColor; GUI.matrix = oldMatrix;
    }
    void Label(Rect rect,string value) { GUI.Label(rect,value,text); }
    void DrawPropertyCard(float width, float height, PlayerState player, PropertyZone land, Color restoreColor)
    {
        float cardWidth = presentation.PropertyCardWidth;
        float cardHeight = presentation.PropertyCardHeight;
        float x = width * .5f - cardWidth * .5f;
        float y = presentation.PropertyCardTop(height);
        Rect card = new Rect(x,y,cardWidth,cardHeight);

        GUI.color = new Color(0f,0f,0f,.8f);
        GUI.DrawTexture(new Rect(x-3f,y-3f,cardWidth+6f,cardHeight+6f),Texture2D.whiteTexture);
        GUI.color = restoreColor;
        Panel(card);

        Color headerColor = land.Owner == null ? new Color(.72f,.53f,.13f) : land.Owner.PlayerColor;
        GUI.color = headerColor;
        GUI.DrawTexture(new Rect(x,y,cardWidth,40f),Texture2D.whiteTexture);
        GUI.color = restoreColor;

        var content = new PropertyCardPresentation(land.PropertyName,land.Price,land.Toll,
            land.Owner == null ? null : land.Owner.DisplayName,player.Money,player.CanAct);
        GUI.Label(new Rect(x+10f,y+2f,cardWidth-20f,36f),content.Title,cardTitle);
        GUI.Label(new Rect(x+12f,y+41f,cardWidth-24f,22f),content.OwnerText,cardStatus);

        GUI.color = new Color(.16f,.19f,.24f,.98f);
        GUI.DrawTexture(new Rect(x+12f,y+65f,132f,34f),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x+156f,y+65f,132f,34f),Texture2D.whiteTexture);
        GUI.color = restoreColor;
        GUI.Label(new Rect(x+14f,y+65f,128f,34f),content.PriceText,cardValue);
        GUI.Label(new Rect(x+158f,y+65f,128f,34f),content.RentText,cardValue);
        GUI.Label(new Rect(x+12f,y+101f,cardWidth-24f,27f),content.ActionText,cardStatus);

        if (land.Owner == null)
        {
            GUI.color = new Color(.25f,.28f,.32f);
            GUI.DrawTexture(new Rect(x+18f,y+134f,cardWidth-36f,10f),Texture2D.whiteTexture);
            GUI.color = player.PlayerColor;
            GUI.DrawTexture(new Rect(x+18f,y+134f,(cardWidth-36f)*player.PurchaseProgress,10f),Texture2D.whiteTexture);
            GUI.color = restoreColor;
        }
    }
    void DrawEventCard(float width,float height,string message,Color restoreColor)
    {
        float cardWidth = presentation.EventCardWidth;
        float cardHeight = presentation.EventCardHeight;
        float x = width*.5f-cardWidth*.5f;
        float y = presentation.EventCardTop(height);
        string title = "EVENT";
        string body = message ?? string.Empty;
        int split = body.IndexOf('\n');
        if (split >= 0)
        {
            title = body.Substring(0,split);
            body = body.Substring(split+1);
        }

        GUI.color = new Color(0f,0f,0f,.8f);
        GUI.DrawTexture(new Rect(x-3f,y-3f,cardWidth+6f,cardHeight+6f),Texture2D.whiteTexture);
        GUI.color = restoreColor;
        Panel(new Rect(x,y,cardWidth,cardHeight));
        GUI.color = new Color(.2f,.55f,.95f,.98f);
        GUI.DrawTexture(new Rect(x,y,cardWidth,42f),Texture2D.whiteTexture);
        GUI.color = restoreColor;
        GUI.Label(new Rect(x+10f,y+2f,cardWidth-20f,38f),title,cardTitle);
        GUI.Label(new Rect(x+16f,y+49f,cardWidth-32f,62f),body,centered);
    }
    static void Panel(Rect rect)
    {
        Color previous = GUI.color;
        GUI.color = new Color(.08f,.1f,.14f,.96f);
        GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color = previous;
    }
}

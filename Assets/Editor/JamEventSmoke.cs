using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in runtime integration check. All mutations are Play Mode only and disappear on exit.
[InitializeOnLoad]
public static class JamEventSmoke
{
    const string Pending = "JamEventSmoke.Pending";
    const string Batch = "JamEventSmoke.Batch";
    const string BatchResult = "JamEventSmoke.BatchResult";
    static int stage;
    static double began;
    static PlayerState source;
    static PlayerEffects effects;
    static float startedAt;
    static float groundY;
    static Keyboard keyboard;
    static PropertyZone purchaseLand;
    static int purchaseMoney;
    static Vector3 fixedCameraPosition;
    static JailZone jailTrap;
    static int batchMoveSteps;
    static JamEventSmoke()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false); stage = 0;
                if (SessionState.GetBool(Batch, false))
                {
                    int result = SessionState.GetInt(BatchResult, 1);
                    SessionState.SetBool(Batch, false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(result);
                }
            }
        };
    }
    // Command-line entry used on a disposable project copy so an open editor is never disturbed.
    public static void BeginBatch()
    {
        SessionState.SetBool(Batch, true);
        SessionState.SetInt(BatchResult, 1);
        EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
        Begin();
    }
    [MenuItem("Game Jam/Start Event Runtime Check")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/MainGame.unity") return;
        stage = 0; began = 0;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("[JamEvent Check] PASS " + message);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
        if (began == 0) began = EditorApplication.timeSinceStartup;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 40) throw new Exception("Timed out waiting for local player/runtime time");
            GameManager game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (stage == 0)
            {
                if (game == null || game.GetLocalPlayer() == null) return;
                source = game.GetLocalPlayer(); effects = source.GetComponent<PlayerEffects>();
                Check(effects != null && game.HasJail && game.HasRaceStart, "prefab and teleport references wired");
                Check(Camera.main != null && Camera.main.orthographic, "fixed board camera is orthographic");
                fixedCameraPosition = Camera.main.transform.position;
                Check(game.GetComponent<GameHud>().SelfArrowVisible, "local YOU arrow visible while waiting");
                game.StartMatch(); stage = 1;
                Check(game.GetComponent<GameHud>().SelfArrowVisible, "local YOU arrow remains during countdown");
            }
            else if (stage == 1)
            {
                if (!game.CanPlay) return;
                Check(source != null && source.IsLocalPlayer, "local player is ready in Playing");
                Check(!game.GetComponent<GameHud>().SelfArrowVisible, "YOU arrow hides when playing");
                BoardEventZone zone = UnityEngine.Object.FindAnyObjectByType<BoardEventZone>();
                Check(zone != null, "event tile exists");
                int money = source.Money;
                zone.ApplyEvent(source, BoardEventKind.Bonus);
                Check(source.Money == money + 30 && source.Score == source.LapCount * 100L + source.Money,
                    "bonus changes wallet and money contributes to score");
                zone.ApplyEvent(source, BoardEventKind.Bill);
                Check(source.Money == money + 10, "bill debits actual wallet");
                zone.ApplyEvent(source, BoardEventKind.JailPass);
                Check(effects.HasJailPass && !effects.TryJail() && !effects.HasJailPass && !effects.IsJailed,
                    "automatic jail pass prevents detention");
                effects.ResetEffects();
                zone.ApplyEvent(source, BoardEventKind.ReturnStart);
                Check(Vector3.Distance(source.transform.position, game.RaceStartPosition) < .1f,
                    "return-start event teleports player");
                zone.ApplyEvent(source, BoardEventKind.SpeedUp);
                Check(Mathf.Approximately(effects.SpeedMultiplier,1.5f), "tailwind applies movement modifier");
                zone.ApplyEvent(source, BoardEventKind.SlowDown);
                Check(Mathf.Approximately(effects.SpeedMultiplier,.6f), "traffic replaces modifier without stacking");
                zone.ApplyEvent(source, BoardEventKind.TollPass);
                Check(source.ConsumeTollPass() && !source.ConsumeTollPass(), "toll pass consumed exactly once");
                effects.ResetEffects();
                jailTrap = UnityEngine.Object.FindAnyObjectByType<JailZone>();
                Check(jailTrap != null, "physical jail trap exists");
                keyboard = Keyboard.current;
                if (!SessionState.GetBool(Batch, false)) Check(keyboard != null, "keyboard exists for physical trap input check");
                // Approach from outside. Batch mode drives the same CharacterController directly because
                // a headless virtual keyboard is not paired to PlayerInput.
                effects.Teleport(jailTrap.transform.position + new Vector3(0f,1.3f,-1.4f));
                if (keyboard != null) InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                batchMoveSteps = 0; startedAt = Time.time; stage = 10;
            }
            else if (stage == 10)
            {
                if (SessionState.GetBool(Batch, false) && !effects.IsJailed && batchMoveSteps < 12)
                {
                    batchMoveSteps++;
                    source.GetComponent<CharacterController>().Move(Vector3.forward * .25f);
                    return;
                }
                if (effects.IsJailed)
                {
                    if (keyboard != null) InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                }
                else if (Time.time-startedAt < 1f) return;
                else if (keyboard != null) InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Check(effects.IsJailed && Vector3.Distance(source.transform.position,game.JailPosition) < .2f,
                    "walking onto the physical trap starts jail");
                effects.ResetEffects();
                BoardEventZone zone = UnityEngine.Object.FindAnyObjectByType<BoardEventZone>();
                zone.ApplyEvent(source, BoardEventKind.Jail);
                Check(effects.IsJailed && !source.CanAct && Vector3.Distance(source.transform.position,game.JailPosition) < .1f,
                    "jail teleports and blocks actions");
                Check(source.GetComponent<PushAbility>().TryPush() == 0, "jailed player cannot push");
                startedAt = Time.time; stage = 2;
            }
            else if (stage == 2)
            {
                if (source == null) throw new Exception("Local player disappeared before release validation");
                if (Time.time - startedAt < 5.15f) return;
                if (SessionState.GetBool(Batch, false)
                    && Vector3.Distance(source.transform.position,game.JailExitPosition) >= .3f) return;
                Check(!effects.IsJailed && source.CanAct && Vector3.Distance(source.transform.position,game.JailExitPosition) < .3f,
                    "five-second sentence releases onto road");
                if (SessionState.GetBool(Batch, false))
                {
                    SessionState.SetBool(Pending,false);
                    SessionState.SetInt(BatchResult,0);
                    Debug.Log("[JamEvent Check] COMPLETE — physical jail trap, detention and release passed.");
                    EditorApplication.isPlaying = false;
                    return;
                }
                effects.ResetEffects();
                effects.Teleport(new Vector3(-10f,1.3f,-4f));
                startedAt = Time.time; stage = 3;
            }
            else if (stage == 3)
            {
                CharacterController capsule = source.GetComponent<CharacterController>();
                if (!capsule.isGrounded) return;
                keyboard = Keyboard.current;
                Check(keyboard != null, "keyboard exists for input pipeline check");
                groundY = source.transform.position.y;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
                startedAt = Time.time; stage = 4;
            }
            else if (stage == 4)
            {
                if (Time.time - startedAt < .12f) return;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Check(source.transform.position.y > groundY + .2f, "Space input produces upward CharacterController movement");
                Check(source.PushCooldownRemaining <= 0f, "Space jump does not trigger push cooldown");
                foreach (PropertyZone land in UnityEngine.Object.FindObjectsByType<PropertyZone>())
                    if (land.Owner == null && land.Price == 20) { purchaseLand = land; break; }
                Check(purchaseLand != null, "affordable property available for real input hold check");
                effects.Teleport(new Vector3(purchaseLand.transform.position.x,1.4f,purchaseLand.transform.position.z));
                purchaseLand.SendMessage("OnTriggerEnter",source.GetComponent<CharacterController>());
                source.UpdatePurchaseHold(false,0f);
                purchaseMoney = source.Money;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));
                startedAt = Time.time; stage = 5;
            }
            else if (stage == 5)
            {
                if (Time.time-startedAt < 1f) return;
                Check(purchaseLand.Owner == null && source.Money == purchaseMoney && source.PurchaseProgress > 0f,
                    "real E input builds progress without early debit");
                Check(source.GetComponent<PlayerKnockback>().ReceivePush(Vector3.forward,1f), "push reaches purchasing player");
                startedAt = Time.time; stage = 6;
            }
            else if (stage == 6)
            {
                if (Time.time-startedAt < .2f) return;
                Check(purchaseLand.Owner == null && source.PurchaseProgress == 0f, "push clears purchase despite E staying held");
                CharacterController body = source.GetComponent<CharacterController>();
                purchaseLand.SendMessage("OnTriggerExit",body);
                purchaseLand.SendMessage("OnTriggerEnter",body);
                source.UpdatePurchaseHold(true,2f);
                Check(purchaseLand.Owner == null && source.PurchaseProgress == 0f,
                    "complete exit and re-entry cannot bypass push interruption without releasing E");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                source.GetComponent<PlayerKnockback>().ResetKnockback();
                startedAt = Time.time; stage = 7;
            }
            else if (stage == 7)
            {
                if (Time.time-startedAt < .15f) return;
                purchaseMoney = source.Money;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));
                startedAt = Time.time; stage = 8;
            }
            else if (stage == 8)
            {
                if (Time.time-startedAt < 2.15f) return;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Check(purchaseLand.Owner == source && source.Money == purchaseMoney-purchaseLand.Price,
                    "two-second real input hold purchases and debits exactly once");
                Check(Vector3.Distance(Camera.main.transform.position,fixedCameraPosition) < .01f,
                    "board camera stays fixed after player teleports and moves");
                source.UpdatePurchaseHold(false,0f);
                // Test the actual rent settlement and effects bridge using the just-purchased land.
                source.AddMoney(-source.Money);
                Check(source.PayMandatory(5) == 5 && source.Money == 5 && purchaseLand.Owner == null && !effects.IsWorking,
                    "rent shortfall sells land at half price and returns it to bank");
                Check(source.PayMandatory(20) == 5 && source.Money == 0 && effects.IsWorking && !source.CanAct,
                    "after all land is sold rent shortfall begins stationary work");
                startedAt = Time.time; stage = 9;
            }
            else if (stage == 9)
            {
                if (Time.time-startedAt < 3.15f) return;
                Check(!effects.IsWorking && source.CanAct && source.Money == 10, "three-second work ends with one ten-dollar wage");
                source.AddMoney(40);
                SessionState.SetBool(Pending,false);
                Debug.Log("[JamEvent Check] COMPLETE — camera/arrow/input hold, events/jail/jump and liquidation/work passed; not a multiplayer or hardware test.");
                JamGameplaySmoke.Run();
                if (SessionState.GetBool(Batch, false))
                {
                    SessionState.SetInt(BatchResult, 0);
                    EditorApplication.isPlaying = false;
                }
            }
        }
        catch (Exception error)
        {
            if (keyboard != null && EditorApplication.isPlaying) InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            SessionState.SetBool(Pending,false);
            Debug.LogError("[JamEvent Check] FAILED " + error.Message);
            if (SessionState.GetBool(Batch, false)) EditorApplication.isPlaying = false;
        }
    }
}

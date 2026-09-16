using System;
using UnityEditor;
using UnityEngine;

// Test-only Editor tool. Creates a temporary passive target, never saves it into the scene.
public static class JamGameplaySmoke
{
    private static PlayerKnockback target;
    private static Vector3 initialPosition;
    private static double checkAt;
    [MenuItem("Game Jam/Run Play Mode Smoke Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Start Play Mode and click Start match before running the smoke test.");
            return;
        }
        GameManager game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        PlayerState source = game == null ? null : game.GetLocalPlayer();
        if (source == null || !game.CanPlay)
        {
            Debug.LogWarning("The smoke test needs a local player in Playing state.");
            return;
        }
        try
        {
            PropertyZone property = source.NearbyProperty;
            if (property != null && (property.Owner != null || property.Price > source.Money)) property = null;
            if (property == null)
            {
                foreach (PropertyZone candidate in UnityEngine.Object.FindObjectsByType<PropertyZone>())
                    if (candidate.Owner == null && candidate.Price <= source.Money) { property = candidate; break; }
                if (property != null)
                {
                    source.GetComponent<PlayerEffects>().Teleport(new Vector3(property.transform.position.x,1.4f,property.transform.position.z));
                    property.SendMessage("OnTriggerEnter",source.GetComponent<CharacterController>());
                }
            }
            Check(property != null && property.Owner == null, "start inside an unowned property for purchase testing");
            int before = source.Money;
            source.UpdatePurchaseHold(false,0f);
            source.UpdatePurchaseHold(true,1.99f);
            Check(property.Owner == null && source.Money == before, "hold does not debit before two seconds");
            source.InterruptPurchase();
            source.UpdatePurchaseHold(true,2f);
            Check(property.Owner == null && source.PurchaseProgress == 0f, "interruption cancels held purchase");
            source.UpdatePurchaseHold(false,0f);
            source.UpdatePurchaseHold(true,2f);
            source.TryBuyNearby();
            Check(property.Owner == source && source.Money == before - property.Price, "purchase and debit");
            source.TryBuyNearby();
            Check(source.Money == before - property.Price, "duplicate purchase does not debit again");

            GameObject dummy = new GameObject("JamSmoke_TemporaryTarget");
            Vector3 forward = source.GetComponent<FollowCamPlayer>().FacingDirection;
            dummy.transform.position = source.transform.position + forward * 1.5f;
            CharacterController collider = dummy.AddComponent<CharacterController>();
            PlayerState visitor = dummy.AddComponent<PlayerState>();
            target = dummy.AddComponent<PlayerKnockback>();
            SphereCollider secondCollider = dummy.AddComponent<SphereCollider>();
            secondCollider.radius = 0.2f;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "TemporaryTargetVisual";
            visual.transform.SetParent(dummy.transform, false);
            visual.GetComponent<Collider>().enabled = false;

            int visitorBefore = visitor.Money;
            int ownerBefore = source.Money;
            // Feed actual handlers real engine colliders to test multi-collider enter/exit bookkeeping.
            // This does not substitute for testing automatic physics callbacks while running a lap.
            property.SendMessage("OnTriggerEnter", collider);
            property.SendMessage("OnTriggerEnter", secondCollider);
            Check(visitor.Money == visitorBefore - property.Toll && source.Money == ownerBefore + property.Toll, "rent charged once for two colliders");
            property.SendMessage("OnTriggerExit", collider);
            property.SendMessage("OnTriggerEnter", collider);
            Check(visitor.Money == visitorBefore - property.Toll, "partial exit does not re-charge");
            property.SendMessage("OnTriggerExit", collider);
            property.SendMessage("OnTriggerExit", secondCollider);
            property.SendMessage("OnTriggerEnter", collider);
            Check(visitor.Money == visitorBefore - property.Toll * 2, "complete exit enables next rent");

            CoinPickup coin = UnityEngine.Object.FindAnyObjectByType<CoinPickup>();
            Check(coin != null, "coin exists");
            coin.ResetPickup();
            int coinBefore = visitor.Money;
            coin.SendMessage("OnTriggerEnter", collider);
            coin.SendMessage("OnTriggerStay", collider);
            Check(visitor.Money == coinBefore + 5, "coin rewards exactly once until respawn");

            LapTracker laps = source.GetComponent<LapTracker>();
            int lapBefore = laps.LapCount;
            laps.passCheckpoint(1); laps.passCheckpoint(2); laps.passCheckpoint(3); laps.passCheckpoint(0);
            Check(laps.LapCount == lapBefore + 1, "checkpoint order completes a lap");
            laps.passCheckpoint(0);
            Check(laps.LapCount == lapBefore + 1, "finish cannot double count");

            Physics.SyncTransforms();
            initialPosition = dummy.transform.position;
            PushAbility push = source.GetComponent<PushAbility>();
            Check(push != null && push.TryPush() == 1 && target.Velocity.sqrMagnitude > 1f, "push hits one target once despite multiple colliders");
            Check(push.TryPush() == 0 && push.CooldownRemaining > 0f, "immediate repeated push blocked");
            checkAt = EditorApplication.timeSinceStartup + 0.3;
            EditorApplication.update -= CheckDisplacement;
            EditorApplication.update += CheckDisplacement;
            Debug.Log("[JamCore Smoke] PASS purchase, duplicate purchase, rent enter/exit, coin, lap and push hit/cooldown. Checking actual CharacterController displacement...");
        }
        catch (Exception e) { Debug.LogError("[JamCore Smoke] FAIL: " + e.Message); }
    }
    private static void CheckDisplacement()
    {
        if (EditorApplication.timeSinceStartup < checkAt) return;
        EditorApplication.update -= CheckDisplacement;
        if (!EditorApplication.isPlaying || target == null) return;
        if (Vector3.Distance(target.transform.position, initialPosition) > 0.1f)
            Debug.Log("[JamCore Smoke] PASS actual knockback displacement. Temporary target and test state disappear when exiting Play Mode; no scene saved.");
        else Debug.LogError("[JamCore Smoke] FAIL: target did not move under knockback.");
    }
    private static void Check(bool condition, string behavior)
    {
        if (!condition) throw new Exception(behavior);
    }
}

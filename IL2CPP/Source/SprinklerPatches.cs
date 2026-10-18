#if FEATURE_SPRINKLER
using HarmonyLib;
using Il2CppFishNet;
using Il2CppScheduleOne.AvatarFramework.Equipping;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;

namespace BotanistUseSprinkler.Runtime;

internal static class SprinklerAutomation
{
    private const float MinimumSuppressionSeconds = 15f;
    private static readonly Dictionary<IntPtr, ActiveSprinkler> ActiveSprinklersByPot = new();

    private sealed class ActiveSprinkler
    {
        internal ActiveSprinkler(Sprinkler sprinkler)
        {
            Sprinkler = sprinkler;
            MinimumEndTime = UnityEngine.Time.realtimeSinceStartup + MinimumSuppressionSeconds;
        }

        internal Sprinkler Sprinkler { get; }
        internal float MinimumEndTime { get; }
    }

    internal static Sprinkler? FindSprinkler(WaterPotBehaviour behaviour)
    {
        Pot? pot = behaviour._growContainer?.TryCast<Pot>();
        return pot is null ? null : DeviceLocator.FindUsableSprinkler(pot);
    }

    internal static void SuppressLinkedPotTasks(Sprinkler sprinkler, Pot targetPot)
    {
        ActiveSprinkler suppression = new(sprinkler);
        bool recordedTarget = false;

        foreach (Pot pot in sprinkler.GetPots())
        {
            if (pot is null)
            {
                continue;
            }

            ActiveSprinklersByPot[pot.Pointer] = suppression;
            recordedTarget |= pot == targetPot;
        }

        if (!recordedTarget)
        {
            ActiveSprinklersByPot[targetPot.Pointer] = suppression;
        }
    }

    internal static bool IsPotTaskSuppressed(Pot pot)
    {
        if (!ActiveSprinklersByPot.TryGetValue(pot.Pointer, out ActiveSprinkler? suppression))
        {
            return false;
        }

        if (UnityEngine.Time.realtimeSinceStartup <= suppression.MinimumEndTime ||
            (suppression.Sprinkler is not null && suppression.Sprinkler.IsSprinkling))
        {
            return true;
        }

        ActiveSprinklersByPot.Remove(pot.Pointer);
        return false;
    }
}

[HarmonyPatch(
    typeof(WaterPotBehaviour),
    "AreTaskConditionsMetForContainer",
    new[] { typeof(GrowContainer) })]
internal static class SprinklerTaskConditionsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(GrowContainer container, ref bool __result)
    {
        if (!RuntimeSettings.SprinklerEnabled || !InstanceFinder.IsServer)
        {
            return true;
        }

        Pot? pot = container?.TryCast<Pot>();
        if (pot is null || !SprinklerAutomation.IsPotTaskSuppressed(pot))
        {
            return true;
        }

        // The sprinkler applies water over time. This lookup uses only state recorded by a
        // successful activation, avoiding scene-wide device searches during save restoration.
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(WaterPotBehaviour), "GetActionEquippable")]
internal static class SprinklerActionEquippablePatch
{
    [HarmonyPrefix]
    private static bool Prefix(WaterPotBehaviour __instance, ref AvatarEquippable __result)
    {
        if (!RuntimeSettings.SprinklerEnabled ||
            !InstanceFinder.IsServer ||
            SprinklerAutomation.FindSprinkler(__instance) is null)
        {
            return true;
        }

        __result = null!;
        return false;
    }
}

[HarmonyPatch(typeof(WaterPotBehaviour), "GetAnimationBool")]
internal static class SprinklerAnimationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(WaterPotBehaviour __instance, ref string __result)
    {
        if (!RuntimeSettings.SprinklerEnabled ||
            !InstanceFinder.IsServer ||
            SprinklerAutomation.FindSprinkler(__instance) is null)
        {
            return true;
        }

        __result = string.Empty;
        return false;
    }
}

[HarmonyPatch(typeof(WaterPotBehaviour), "GetActionDuration")]
internal static class SprinklerActionDurationPatch
{
    [HarmonyPostfix]
    private static void Postfix(WaterPotBehaviour __instance, ref float __result)
    {
        if (RuntimeSettings.SprinklerEnabled &&
            InstanceFinder.IsServer &&
            SprinklerAutomation.FindSprinkler(__instance) is not null)
        {
            __result = 0f;
        }
    }
}

[HarmonyPatch(typeof(WaterPotBehaviour), "OnActionSuccess")]
internal static class SprinklerActionSuccessPatch
{
    [HarmonyPrefix]
    private static bool Prefix(WaterPotBehaviour __instance)
    {
        if (!RuntimeSettings.SprinklerEnabled || !InstanceFinder.IsServer)
        {
            return true;
        }

        Sprinkler? sprinkler = SprinklerAutomation.FindSprinkler(__instance);
        if (sprinkler is null)
        {
            return true;
        }

        if (!sprinkler.IsSprinkling)
        {
            sprinkler.SendWater();
        }

        Pot? targetPot = __instance._growContainer?.TryCast<Pot>();
        if (targetPot is not null)
        {
            SprinklerAutomation.SuppressLinkedPotTasks(sprinkler, targetPot);
        }

        RuntimeSettings.Debug($"Used sprinkler at {sprinkler._originCoordinate}.");

        // The sprinkler owns the watering result. The game's coroutine still ends and disables
        // the botanist behavior through the normal GrowContainerBehaviour lifecycle.
        return false;
    }
}
#endif

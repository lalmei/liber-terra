using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace LiberTerra.Items;

/// <summary>
/// Shared helpers for rock-like book throws and tap-to-open.
/// </summary>
public static class BookThrowUtil
{
    public const string ForceOpenAttr = "liberterra-force-open";

    /// <summary>
    /// Set by <see cref="CollectibleBehaviorBookThrowable"/> when it actually armed a windup on
    /// mouse-down, so the matching stop knows the use is ours to finish.
    ///
    /// Vanilla's own "aiming" flag cannot answer that: releasing RMB makes the client call
    /// OnHeldInteractCancel(ReleasedMouse) — which clears "aiming" — and only then
    /// OnHeldInteractStop. The server, handed a StopHeldItemUse packet, never runs that cancel, so
    /// "aiming" is 1 there and 0 on the client for the very same release.
    /// </summary>
    public const string ArmedAttr = "liberterra-book-armed";
    /// <summary>
    /// Vanilla stones throw after 0.35s. Books share that button with reading, so the charge
    /// has to outlast a slow click or the volume flies instead of opening.
    /// </summary>
    public const float DefaultWindupSec = 1.0f;

    /// <summary>What vanilla's throwable behavior and cancel path both name, so we can too.</summary>
    public const string AimAnimation = "aim";

    public static bool IsForceOpen(EntityAgent byEntity)
        => byEntity.Attributes.GetInt(ForceOpenAttr) == 1;

    public static void SetForceOpen(EntityAgent byEntity, bool value)
        => byEntity.Attributes.SetInt(ForceOpenAttr, value ? 1 : 0);

    public static bool IsArmed(EntityAgent byEntity)
        => byEntity.Attributes.GetInt(ArmedAttr) == 1;

    public static void SetArmed(EntityAgent byEntity, bool value)
        => byEntity.Attributes.SetInt(ArmedAttr, value ? 1 : 0);

    /// <summary>
    /// Ends a throw windup by hand, for the paths that never get a stop.
    ///
    /// The client drops a held interaction the moment the held slot empties: it clears HandUse and
    /// nothing else — no OnHeldInteractStop, no OnHeldInteractCancel, no packet — and the server
    /// ignores any stop that names an empty slot. Books are maxstacksize 1, so putting one in a
    /// pile always empties the slot, and whatever OnHeldInteractStart left on the entity stays
    /// there: the player holds the throw pose until some later aim runs start to stop. So every
    /// path that consumes the held book stops its own aim.
    /// </summary>
    public static void StopAiming(EntityAgent byEntity)
    {
        SetArmed(byEntity, false);
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation(AimAnimation);
    }

    /// <summary>
    /// Opens a lore book the same way as a short RMB (discovery + readonly GUI).
    /// </summary>
    public static void TryOpenBook(
        CollectibleObject collObj,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection? blockSel,
        EntitySelection? entitySel)
    {
        if (slot.Itemstack is null || collObj is null)
        {
            return;
        }

        SetForceOpen(byEntity, true);
        try
        {
            var handling = EnumHandHandling.NotHandled;
            collObj.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, true, ref handling);
        }
        finally
        {
            SetForceOpen(byEntity, false);
        }
    }

    /// <summary>
    /// Spawns a vanilla thrownitem projectile carrying <paramref name="projectileStack"/>.
    /// </summary>
    public static bool TryThrowItemStack(
        EntityAgent byEntity,
        ItemStack projectileStack,
        ProjectileJsonConfig config,
        string thrownProjectileCode = "thrownitem",
        AssetLocation? thrownSound = null,
        string throwAnimation = "throw",
        float projectileSpeed = 0.5f,
        float accuracy = 0.75f,
        bool randomThrowYaw = true,
        float verticalOffset = 0.1f,
        float horizontalOffset = 0.4f,
        float forwardOffset = -0.21f,
        float parallaxDistance = 20f)
    {
        thrownSound ??= new AssetLocation("game:sounds/player/throw");

        var player = (byEntity as EntityPlayer)?.Player;
        byEntity.World.PlaySoundAt(thrownSound, byEntity, player, false, 8f);

        var entityType = byEntity.World.GetEntityType(new AssetLocation(thrownProjectileCode));
        if (entityType is null)
        {
            byEntity.World.Logger.Warning(
                "Liber Terra: thrown entity type {0} does not exist, book not thrown",
                thrownProjectileCode);
            return false;
        }

        var entity = byEntity.World.ClassRegistry.CreateEntity(entityType);
        if (entity is not IProjectile projectile)
        {
            byEntity.World.Logger.Warning(
                "Liber Terra: thrown entity type {0} does not implement IProjectile",
                thrownProjectileCode);
            return false;
        }

        if (randomThrowYaw)
        {
            entity.Pos.Yaw = (float)(Math.PI * 2 * byEntity.World.Rand.NextDouble());
        }

        projectile.SetFromConfig(config);
        projectile.FiredBy = byEntity;
        projectile.ProjectileStack = projectileStack;

        EntityProjectileBase.SpawnProjectile(
            entity,
            byEntity,
            projectileSpeed,
            accuracy,
            verticalOffset,
            horizontalOffset,
            forwardOffset,
            parallaxDistance);

        byEntity.StartAnimation(throwAnimation);
        return true;
    }

    public static ProjectileJsonConfig DefaultBookProjectileConfig()
        => new()
        {
            Damage = 1,
            DamageType = EnumDamageType.BluntAttack,
            DropOnImpactChance = 1
        };
}

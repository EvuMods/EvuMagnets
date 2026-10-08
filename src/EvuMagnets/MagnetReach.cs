using EvuMagnets.Core;

namespace EvuMagnets;

/// <summary>
/// Shares each player's current pickup reach on their ZDO so other clients know who can take a drop.
/// Remote inventories are not synced, so this is the only way to see another player's magnet.
/// </summary>
internal static class MagnetReach
{
    static readonly int ReachHash = "EvuMagnets.Reach".GetStableHashCode();
    static float _published = -1f;
    static ZDO? _publishedTo;

    public static void Publish(Player player, float range)
    {
        if (player == null)
        {
            return;
        }

        var view = player.m_nview;
        if (view == null || !view.IsValid() || !view.IsOwner())
        {
            return;
        }

        var zdo = view.GetZDO();
        if (ReferenceEquals(zdo, _publishedTo) && range == _published)
        {
            return;
        }

        zdo.Set(ReachHash, range);
        _published = range;
        _publishedTo = zdo;
    }

    public static float Of(Player other)
    {
        var view = other != null ? other.m_nview : null;
        if (view == null || !view.IsValid())
        {
            return PickupRules.VanillaRange;
        }

        return PickupRules.Reach(view.GetZDO().GetFloat(ReachHash, 0f));
    }
}

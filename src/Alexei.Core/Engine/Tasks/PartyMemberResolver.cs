using Alexei.Core.GameState;

namespace Alexei.Core.Engine.Tasks;

internal static class PartyMemberResolver
{
    public static PartyMember? ResolveConfiguredMember(GameWorld world, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return null;

        if (TryParseObjectId(selector, out var objectId) && TryGetKnownActorById(world, objectId, out var byId))
            return byId;

        return world.Party.Values.FirstOrDefault(member =>
                   string.Equals(member.Name, selector, StringComparison.OrdinalIgnoreCase))
               ?? world.Characters.Values.FirstOrDefault(member =>
                   string.Equals(member.Name, selector, StringComparison.OrdinalIgnoreCase));
    }

    public static PartyMember? ResolveLeaderByObjectId(GameWorld world)
    {
        if (world.PartyLeaderObjectId == 0)
            return null;

        return TryGetKnownActorById(world, world.PartyLeaderObjectId, out var leader)
            ? leader
            : null;
    }

    public static PartyMember? ResolveSoleMember(GameWorld world) =>
        world.Party.Count == 1 ? world.Party.Values.First() : null;

    public static bool TryGetKnownActorById(GameWorld world, int objectId, out PartyMember actor)
    {
        actor = default!;
        if (objectId == 0)
            return false;

        if (world.Party.TryGetValue(objectId, out var partyMember) && partyMember != null)
        {
            actor = partyMember;
            return true;
        }

        if (world.Characters.TryGetValue(objectId, out var knownMember) && knownMember != null)
        {
            actor = knownMember;
            return true;
        }

        return false;
    }

    public static string DescribeMember(PartyMember member) =>
        string.IsNullOrWhiteSpace(member.Name) ? $"obj:{member.ObjectId}" : $"{member.Name}#{member.ObjectId}";

    private static bool TryParseObjectId(string selector, out int objectId)
    {
        var trimmed = selector.Trim();
        if (trimmed.StartsWith("obj:", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(trimmed[4..], out objectId);

        if (trimmed.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(trimmed[3..], out objectId);

        return int.TryParse(trimmed, out objectId);
    }
}


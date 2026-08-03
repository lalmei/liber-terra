using LiberTerra.Lore;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace LiberTerra.Commands;

public sealed class LiberTerraServerCommands
{
    private static readonly string[] BookColors =
    [
        "aged-orangebrown",
        "aged-orange",
        "aged-darkgreen",
        "aged-darkgray",
        "aged-cherryred",
        "aged-brickred",
        "aged-darkolive",
        "aged-darkbeige",
        "aged-olive",
        "aged-purpleorange",
        "aged-gray"
    ];

    private readonly Func<LiberTerraCatalog?> catalogProvider;
    private ICoreServerAPI? api;

    public LiberTerraServerCommands(Func<LiberTerraCatalog?> catalogProvider)
    {
        this.catalogProvider = catalogProvider;
    }

    public void Register(ICoreServerAPI api)
    {
        this.api = api;
        api.ChatCommands.Create("liberterra")
            .WithDescription("Liber Terra lore library commands.")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(_ => TextCommandResult.Success(
                "Liber Terra: /liberterra list, /liberterra give <code>, /liberterra giveall <baseCode>"))
            .BeginSubCommand("list")
                .WithDescription("List available Liber Terra volumes.")
                .HandleWith(_ => TextCommandResult.Success(ListWorks()))
            .EndSubCommand()
            .BeginSubCommand("give")
                .RequiresPrivilege(Privilege.give)
                .RequiresPlayer()
                .WithDescription("Give a complete Liber Terra lore book volume.")
                .WithArgs(api.ChatCommands.Parsers.Word("code"))
                .HandleWith(GiveOne)
            .EndSubCommand()
            .BeginSubCommand("giveall")
                .RequiresPrivilege(Privilege.give)
                .RequiresPlayer()
                .WithDescription("Give every volume for a Liber Terra base work.")
                .WithArgs(api.ChatCommands.Parsers.Word("baseCode"))
                .HandleWith(GiveAll)
            .EndSubCommand();
    }

    private string ListWorks()
    {
        var catalog = catalogProvider();
        if (catalog is null || catalog.Works.Count == 0)
        {
            return "Liber Terra catalog is not loaded.";
        }

        var lines = catalog.Works
            .OrderBy(work => work.Group)
            .ThenBy(work => work.BaseCode)
            .ThenBy(work => work.Volume)
            .Select(work =>
                $"{work.Code} — {work.Title} [{work.Group}] pieces={work.PieceCount}")
            .ToList();
        return "Liber Terra volumes:\n" + string.Join("\n", lines);
    }

    private TextCommandResult GiveOne(TextCommandCallingArgs args)
    {
        if (api is null)
        {
            return TextCommandResult.Error("Server API unavailable.");
        }

        var catalog = catalogProvider();
        if (catalog is null)
        {
            return TextCommandResult.Error("Liber Terra catalog is not loaded.");
        }

        var code = args[0]?.ToString()?.Trim() ?? "";
        if (!catalog.ByCode.TryGetValue(code, out var work))
        {
            // Allow giving by base code when there is exactly one volume.
            var matches = catalog.Works
                .Where(entry => string.Equals(entry.BaseCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1)
            {
                work = matches[0];
            }
            else if (matches.Count > 1)
            {
                return TextCommandResult.Error(
                    $"'{code}' has {matches.Count} volumes. Use a volume code such as '{matches[0].Code}' or /liberterra giveall {code}.");
            }
            else
            {
                return TextCommandResult.Error($"Unknown Liber Terra code '{code}'. Try /liberterra list.");
            }
        }

        var player = args.Caller.Player;
        var stack = CreateCompleteBook(api, work);
        if (!player.InventoryManager.TryGiveItemstack(stack))
        {
            api.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
        }

        return TextCommandResult.Success($"Gave Liber Terra book: {work.Title} ({work.Code}).");
    }

    private TextCommandResult GiveAll(TextCommandCallingArgs args)
    {
        if (api is null)
        {
            return TextCommandResult.Error("Server API unavailable.");
        }

        var catalog = catalogProvider();
        if (catalog is null)
        {
            return TextCommandResult.Error("Liber Terra catalog is not loaded.");
        }

        var baseCode = args[0]?.ToString()?.Trim() ?? "";
        var matches = catalog.Works
            .Where(entry => string.Equals(entry.BaseCode, baseCode, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(entry.Code, baseCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Volume)
            .ToList();
        if (matches.Count == 0)
        {
            return TextCommandResult.Error($"Unknown Liber Terra base code '{baseCode}'. Try /liberterra list.");
        }

        var player = args.Caller.Player;
        foreach (var work in matches)
        {
            var stack = CreateCompleteBook(api, work);
            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                api.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }
        }

        return TextCommandResult.Success($"Gave {matches.Count} Liber Terra volume(s) for '{matches[0].BaseCode}'.");
    }

    public static ItemStack CreateCompleteBook(ICoreAPI api, LiberTerraWork work)
    {
        var color = BookColors[Math.Abs(work.Code.GetHashCode()) % BookColors.Length];
        var item = api.World.GetItem(new AssetLocation("game", $"lore-book-{color}"))
            ?? throw new InvalidOperationException($"Missing lore book item game:lore-book-{color}");
        var stack = new ItemStack(item);

        var attrs = stack.Attributes;
        attrs.SetString("category", work.Code);
        attrs.SetString("discoveryCode", work.Code);
        attrs.SetString("titleCode", work.TitleCode);

        var textCodes = new StringArrayAttribute(work.TextCodes.ToArray());
        attrs["textCodes"] = textCodes;

        var chapterIds = new IntArrayAttribute(Enumerable.Range(0, work.TextCodes.Count).ToArray());
        attrs["chapterIds"] = chapterIds;

        return stack;
    }
}

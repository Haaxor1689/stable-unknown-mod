using System.Reflection;
using System.Reflection.Emit;
using Allumeria.Blocks.Blocks;
using Allumeria.DataManagement.Saving;
using Allumeria.Items;
using HarmonyLib;
using Logger = Allumeria.Logger;

namespace StableUnknown.Patches;

[HarmonyPatch(typeof(PaletteConstructor), nameof(PaletteConstructor.LoadPalette))]
internal static class PaletteConstructorPatch
{
    // Replacement for: blockPalette[index] = Block.unknown.intID;
    private static ushort GetUnknownBlockID(string str)
    {
        Logger.Warn($"Unknown block type '{str}' encountered during palette loading");
        GrowBlockArrayIfFull();
        GrowItemArrayIfFull();

        var block = new Block(str)
            .SetTexture("deprecated")
            .MakeSolid()
            .SetMaterial(BlockMaterial.plant)
            .Hide();
        block.textureSlots = Block.unknown.textureSlots;
        block.item.SetSprite("deprecated");
        block.item.translatedName = str;
        block.item.itemSprite = Block.unknown.item.itemSprite;
        return block.intID;
    }

    // Replacement for: itemPalette[index] = Block.unknown.item.itemID;
    private static int GetUnknownItemID(string str)
    {
        Logger.Warn($"Unknown item type '{str}' encountered during palette loading");
        GrowItemArrayIfFull();

        var item = new Item(str).SetSprite("deprecated").Hide();
        item.translatedName = str;
        item.itemSprite = Block.unknown.item.itemSprite;
        return item.itemID;
    }

    // Make sure blocks and items are resized to fit again after palette loading.
    private static void Postfix()
    {
        Block.FitBlockArray();
        Item.FitItemArray();
    }

    // Grows blocks by 1/6th once it's full, instead of resizing to fit exactly every load.
    private static void GrowBlockArrayIfFull()
    {
        if (Block.blocks.Length == Block.totalBlockCount)
            Array.Resize(ref Block.blocks, Block.blocks.Length + 1);
    }

    // Grows items by 1/6th once it's full, instead of resizing to fit exactly every load.
    private static void GrowItemArrayIfFull()
    {
        if (Item.items.Length == Item.totalItemCount)
            Array.Resize(ref Item.items, Item.items.Length + 1);
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        List<CodeInstruction> codes = new(instructions);

        FieldInfo unknownField = AccessTools.Field(typeof(Block), nameof(Block.unknown));
        FieldInfo intIDField = AccessTools.Field(typeof(Block), nameof(Block.intID));
        FieldInfo itemField = AccessTools.Field(typeof(Block), nameof(Block.item));
        FieldInfo itemIDField = AccessTools.Field(typeof(Item), nameof(Item.itemID));
        MethodInfo blockIDReplacement = AccessTools.Method(
            typeof(PaletteConstructorPatch),
            nameof(GetUnknownBlockID)
        );
        MethodInfo itemIDReplacement = AccessTools.Method(
            typeof(PaletteConstructorPatch),
            nameof(GetUnknownItemID)
        );

        // "str" is loaded right before each `str.Equals(...)` check (once per foreach loop),
        // clone those loads to get access to the same local later in each loop body.
        MethodInfo stringEquals = AccessTools.Method(
            typeof(string),
            nameof(string.Equals),
            new[] { typeof(string) }
        );
        List<CodeInstruction> strLoads = new();
        for (int i = 1; i < codes.Count; i++)
        {
            if (codes[i].Calls(stringEquals))
            {
                strLoads.Add(codes[i - 1].Clone());
            }
        }

        if (strLoads.Count != 2)
        {
            throw new InvalidOperationException(
                $"Expected 2 uses of string.Equals for 'str', found {strLoads.Count}"
            );
        }

        ReplaceFieldChain(
            codes,
            blockIDReplacement,
            strLoads[0],
            i =>
                codes[i].Is(OpCodes.Ldsfld, unknownField)
                && codes[i + 1].Is(OpCodes.Ldfld, intIDField),
            chainLength: 2
        );

        ReplaceFieldChain(
            codes,
            itemIDReplacement,
            strLoads[1],
            i =>
                codes[i].Is(OpCodes.Ldsfld, unknownField)
                && codes[i + 1].Is(OpCodes.Ldfld, itemField)
                && codes[i + 2].Is(OpCodes.Ldfld, itemIDField),
            chainLength: 3
        );

        return codes;
    }

    private static void ReplaceFieldChain(
        List<CodeInstruction> codes,
        MethodInfo replacement,
        CodeInstruction strLoad,
        Func<int, bool> isMatch,
        int chainLength
    )
    {
        for (int i = 0; i < codes.Count - chainLength + 1; i++)
        {
            if (!isMatch(i))
            {
                continue;
            }

            codes.RemoveRange(i, chainLength);
            codes.Insert(i, new CodeInstruction(OpCodes.Call, replacement));
            codes.Insert(i, strLoad);
            return;
        }

        throw new InvalidOperationException($"Pattern not found for {replacement.Name}");
    }
}

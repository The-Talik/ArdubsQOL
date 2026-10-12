using ArdubsQOL;

internal static class RodPoolPolicyTests
{
    private sealed class Rod
    {
        internal Rod(string type, int charges, int count = 1)
        {
            Type = type;
            Charges = charges;
            Count = count;
        }
        internal string Type;
        internal int Charges;
        internal int Count;
    }

    internal static void Run()
    {
        var inventory = new[]
        {
            new Rod("LesserExtend", 3), new Rod("LesserExtend", 3), new Rod("NormalExtend", 3)
        };
        var lesser = inventory.Where(rod => rod.Type == "LesserExtend").ToArray();
        var normal = inventory.Where(rod => rod.Type == "NormalExtend").ToArray();
        int Charges(IEnumerable<Rod> rods) => RodPoolRules.TotalCharges(rods, rod => rod.Count, rod => rod.Charges);
        Assert(Charges(lesser) == 6 && Charges(normal) == 3, "Rod grades must have separate pooled charges.");
        // Alternating casters read/debit the same inventory; no per-caster copies.
        for (var use = 0; use < 6; use++)
        {
            var source = RodPoolRules.FindChargeSource(lesser, rod => rod.Charges);
            Assert(source != null, "Every available shared charge must be usable.");
            source!.Charges--;
            Assert(Charges(lesser) == 5 - use, "Each caster's use must debit exactly one shared charge.");
        }
        Assert(RodPoolRules.FindChargeSource(lesser, rod => rod.Charges) == null,
            "An exhausted pool must not grant another caster a free use.");
        Assert(Charges(normal) == 3, "Exhausting lesser rods must not consume regular rod charges.");
        foreach (var rod in lesser) rod.Charges = 3;
        Assert(Charges(lesser) == 6, "Restored real rod charges must restore the shared pool.");
        Assert(Charges(new[] { new Rod("LesserExtend", 3, 2) }) == 6,
            "Stacked copies must contribute each rod's charges.");
        Assert(Charges(new[] { new Rod("LesserExtend", -1), new Rod("LesserExtend", 3, 0) }) == 0,
            "Invalid or empty stacks must not create charges.");
        Assert(Charges(new[] { new Rod("LesserExtend", int.MaxValue, 2), new Rod("LesserExtend", 3) }) == int.MaxValue,
            "Large counts must not overflow into negative charges.");

        foreach (var grade in new[] { "Lesser", "Normal", "Greater" })
        foreach (var type in new[] { "Bolster", "Empower", "Extend", "Maximize", "Persistent", "Quicken", "Reach", "Selective", "Intensified", "Piercing" })
            Assert(RodPoolRules.IsOrdinaryRod($"MetamagicRod{grade}{type}ToggleAbility"),
                "An ordinary metamagic rod type was omitted.");
        foreach (var name in new[] { "MetamagicRodMasterpieceToggleAbility", "MetamagicRodLesserKineticToggleAbility",
            "AmberLoveRodToggleAbility", "RodOfMysticismToggleAbility", "MetamagicRodGreaterQuickenExtraChargesToggleAbility" })
            Assert(!RodPoolRules.IsOrdinaryRod(name), "Unique or non-spell rods must not enter ordinary rod pools.");
        Assert(!RodPoolRules.IsOrdinaryRod(null), "An item without a rod toggle must not enter the pool.");

        Assert(RodPoolRules.ShouldSpendCharge(true, true, false, false, false, false),
            "An actual eligible cast must spend one shared charge.");
        Assert(!RodPoolRules.ShouldSpendCharge(false, true, false, false, false, false),
            "An ineligible spell must not consume a rod charge.");
        Assert(!RodPoolRules.ShouldSpendCharge(true, false, false, false, false, false),
            "A preview, cancelled cast, or invalid-target cast must not spend charges.");
        Assert(!RodPoolRules.ShouldSpendCharge(true, true, true, false, false, false),
            "A forced non-cast must not spend charges.");
        Assert(!RodPoolRules.ShouldSpendCharge(true, true, false, true, false, false),
            "A failed UMD attempt must not spend charges.");
        Assert(!RodPoolRules.ShouldSpendCharge(true, true, false, false, true, false),
            "A duplicated spell must not double-charge its original cast.");
        Assert(!RodPoolRules.ShouldSpendCharge(true, true, false, false, false, true),
            "Delivering an already-charged touch spell must not spend a second charge.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using ArdubsQOL;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

Assert(ActionRules.CanActivate(true, true, true, false), "A fresh move action should permit activation.");
Assert(!ActionRules.CanActivate(true, true, true, true), "A previously spent move action must block activation.");
Assert(!ActionRules.CanActivate(false, true, true, false), "Real-time combat must not permit activation.");
Assert(!ActionRules.CanActivate(true, false, true, false), "Out-of-combat activation must be unavailable.");
Assert(!ActionRules.CanActivate(true, true, false, false), "Activation outside the caster's turn must be unavailable.");

var firstTurn = TimeSpan.FromSeconds(12);
var laterTurn = TimeSpan.FromSeconds(18);
Assert(
    ActionRules.ReservationAlreadyCoversTurn(true, firstTurn, firstTurn),
    "The initial round callback on the activation turn must keep the existing reservation.");
Assert(
    !ActionRules.ReservationAlreadyCoversTurn(true, firstTurn, laterTurn),
    "A later turn must reserve a new move action.");
Assert(
    !ActionRules.ReservationAlreadyCoversTurn(false, firstTurn, firstTurn),
    "An unreserved action must not be treated as covered.");

Assert(ActionRules.ReleaseMoveCooldown(3f, true) == 0f, "Turning the aura off should return its reserved move action.");
Assert(ActionRules.ReleaseMoveCooldown(6f, true) == 3f, "Turning the aura off must not refund another spent move action.");
Assert(ActionRules.ReleaseMoveCooldown(3f, false) == 3f, "No reservation means no refund.");

Assert(
    ActionRules.ShouldUseStandardActionForManualChant(true, true, true),
    "Manual Chant should use a standard action while its aura is active.");
Assert(
    !ActionRules.ShouldUseStandardActionForManualChant(true, true, false),
    "Manual Chant should keep its normal action type without a matching aura.");
Assert(
    !ActionRules.ShouldUseStandardActionForManualChant(true, false, true),
    "The action override must not affect real-time combat.");
Assert(
    !ActionRules.ShouldUseStandardActionForManualChant(false, true, true),
    "Disabling Chant Aura must also disable the manual action override.");

var freezeNow = TimeSpan.FromMinutes(5);
Assert(
    ActionRules.PreserveExternalDurationExtension(
        TimeSpan.FromSeconds(12),
        freezeNow,
        freezeNow + TimeSpan.FromSeconds(18)) == TimeSpan.FromSeconds(18),
    "A normal manual Chant extension must become the newly frozen remaining duration.");
Assert(
    ActionRules.PreserveExternalDurationExtension(
        TimeSpan.FromSeconds(12),
        freezeNow,
        freezeNow + TimeSpan.FromSeconds(11)) == TimeSpan.FromSeconds(12),
    "Ordinary countdown between aura updates must not reduce a frozen duration.");

var now = TimeSpan.FromMinutes(10);
var paidIntervalEnd = now + TimeSpan.FromSeconds(37);
Assert(
    ShifterAspectRules.GetPaidIntervalEnd(true, true, true, true, now, paidIntervalEnd) == paidIntervalEnd,
    "Turning off a paid Shifter Aspect should preserve the remainder of its current minute.");
Assert(
    ShifterAspectRules.GetPaidIntervalEnd(false, true, true, true, now, paidIntervalEnd) == null,
    "The Shifter Aspect option must be able to disable the behavior.");
Assert(
    ShifterAspectRules.GetPaidIntervalEnd(true, false, true, true, now, paidIntervalEnd) == null,
    "Unrelated once-per-minute abilities must not be changed.");
Assert(
    ShifterAspectRules.GetPaidIntervalEnd(true, true, false, true, now, paidIntervalEnd) == null,
    "Shifter abilities without once-per-minute spending must not be changed.");
Assert(
    ShifterAspectRules.GetPaidIntervalEnd(true, true, true, false, now, paidIntervalEnd) == null,
    "A free Shifter Aspect with no prepaid deadline must end normally.");
Assert(
    ShifterAspectRules.GetPaidIntervalEnd(true, true, true, true, now, now) == null,
    "An exhausted Shifter Aspect interval must not be extended.");

Assert(
    CasterLabelRules.ShouldAnnotate(true, 1, true),
    "A known Evil Eye should show its hostile caster even when only one copy is active.");
Assert(
    CasterLabelRules.ShouldAnnotate(false, 2, true),
    "An ambiguous duplicate debuff should show its hostile caster.");
Assert(
    !CasterLabelRules.ShouldAnnotate(false, 1, true),
    "An ordinary single debuff should not add hover-inspector clutter.");
Assert(
    !CasterLabelRules.ShouldAnnotate(true, 2, false),
    "A buff without a hostile caster should not receive a misleading caster label.");
Assert(
    AccursedHexLabelRules.Format("Slumber", "Ember") == "Accursed Hex retry: Slumber (Ember)",
    "Accursed Hex hover text should identify both the retry ability and its caster.");
Assert(
    AccursedHexLabelRules.ShouldDisplayRetry(true, true),
    "A passed save against a once-per-target hex should display an Accursed Hex retry.");
Assert(
    !AccursedHexLabelRules.ShouldDisplayRetry(true, false),
    "A repeatable hex must not display a misleading Accursed Hex retry.");
Assert(
    !AccursedHexLabelRules.ShouldDisplayRetry(false, true),
    "A failed save must not display an Accursed Hex retry.");
Assert(
    SuspensionLabelRules.Apply("Evil Eye — Saving Throws", AuraSuspensionKind.Chant)
        == "Evil Eye — Saving Throws (Chant)",
    "A Shaman hex suspended by its aura should display the Chant marker.");
Assert(
    SuspensionLabelRules.Apply("Protective Luck", AuraSuspensionKind.Cackle)
        == "Protective Luck (Cackle)",
    "A Witch hex suspended by its aura should display the Cackle marker.");
Assert(
    SuspensionLabelRules.Apply("Evil Eye (Camellia) (Chant)", AuraSuspensionKind.Cackle)
        == "Evil Eye (Camellia) (Cackle)",
    "Refreshing an inspector row should replace rather than duplicate its suspension marker.");
Assert(
    SuspensionLabelRules.Apply("Evil Eye (Camellia) (Chant)", AuraSuspensionKind.None)
        == "Evil Eye (Camellia)",
    "An inspector row should lose its marker when the hex is no longer suspended.");
Assert(
    HoverDurationRules.Append("Evil Eye — Saving Throws", false, TimeSpan.FromSeconds(6), "6s")
        == "Evil Eye — Saving Throws 6s",
    "A timed hover-inspector entry should include its compact remaining duration.");
Assert(
    HoverDurationRules.Append("Evil Eye — Saving Throws", false, TimeSpan.Zero, "")
        == "Evil Eye — Saving Throws 0s",
    "An attached zero-time entry should remain visible and identify the timing mismatch.");
Assert(
    HoverDurationRules.Append("Permanent effect", true, TimeSpan.MaxValue, "Permanent")
        == "Permanent effect",
    "A permanent hover-inspector entry should not receive a duration suffix.");

Assert(
    NegativeLevelRecoveryRules.ShouldRecover(true, true, false, true, false, true, 7, 8),
    "A completed normal character level-up should permit one negative-level recovery.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(false, true, false, true, false, true, 7, 8),
    "The negative-level recovery option must default to and remain capable of being disabled.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(true, false, false, true, false, true, 7, 8),
    "Character creation and mythic advancement must not trigger negative-level recovery.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(true, true, true, true, false, true, 7, 8),
    "Rebuilding levels during a respec must not trigger negative-level recovery.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(true, true, false, false, false, true, 7, 8),
    "Pets, summons, and non-roster units must not trigger negative-level recovery.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(true, true, false, true, true, true, 7, 8),
    "A repeated commit call must not trigger negative-level recovery twice.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(true, true, false, true, false, false, 7, 8),
    "A cancelled or failed level-up must not trigger negative-level recovery.");
Assert(
    !NegativeLevelRecoveryRules.ShouldRecover(true, true, false, true, false, true, 8, 8),
    "A level-up callback without a character-level increase must not trigger recovery.");

RodPoolPolicyTests.Run();
QuickbarTooltipPolicyTests.Run();
Console.WriteLine("Ardub's QOL policy tests passed.");

using System.Linq;
using ReBot.API;
using System;
using Avoloos.Warlock;

namespace ReBot
{
    [Rotation(
        "Warlock Destruction - Icy Veins Profile",
        "Avoloos",
        WoWClass.Warlock,
        Specialization.WarlockDestruction,
        40
    )]
    public class AvoloosWarlockDestructionIcyVeins : WarlockBaseRotation
    {
        public AvoloosWarlockDestructionIcyVeins()
        {
            GroupBuffs = new[] {
                "Dark Intent",
                ( CurrentBotName == "PvP" ? "Create Soulwell" : null )
            };
            PullSpells = new[] {
                "Conflagrate"
            };
            UsePet = true;
        }

        bool doMultitargetRotation(int mobsInFrontOfMe)
        {
            int burningEmbers = Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers);

            if (CastOnTerrain(
                    "Shadowfury",
                    Target.Position,
                    () => Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12) > 2
                ))
                return true;
           
            // Priority #1
            if (HasSpell("Aftermath") && CastOnTerrainPreventDouble("Rain of Fire", Target.Position, null, 7500))
                return true;

            // Priority #2
            if (
                mobsInFrontOfMe >= 3// Got a Group
                && HasFelguard()// and Has a Felguard
                && Cast("Command Demon") && HasGlobalCooldown())
                return true;

            if (
                SpellCooldown("Havoc") <= 0.01 && burningEmbers >= 1 && mobsInFrontOfMe < 12) {
                // Dont waste Havoc apply it to one of the mid-enemies (high max health, low current health)
                var havocAdd = Adds.OrderByDescending(x => x.Health).FirstOrDefault(x => x.HealthFraction <= 0.4f) ?? Adds.FirstOrDefault();
                if (Cast("Havoc", havocAdd))
                    return true;
            }

            // cast Chaosbolt or shadowburn on target as soon as possible and if feasible
            if (Adds.Count(x => x.HasAura("Havoc", true)) > 0) {
                if (Cast("Shadowburn", () => mobsInFrontOfMe < 12))
                    return true; 
                if (Cast("Chaos Bolt", () => mobsInFrontOfMe < 6))
                    return true; 
            }

            // Priority #3
            var countAddsInRange = Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12);
            if (HasSpell("Fire and Brimstone")
                && (
                    ( burningEmbers >= 2 && countAddsInRange > 2 )
                    || ( burningEmbers >= 1 && countAddsInRange >= 10 )
                )) {
                // Ensure Fire and Brimstone!
                CastSelf("Fire and Brimstone", () => !HasAura("Fire and Brimstone"));

                if (CastSpellOnAdds("Conflagrate"))
                    return true;
                if (CastSpellPreventDoubleOnAdds(
                        "Immolate",
                        add => Adds.Count(y => !y.HasAura("Immolate") && y.HpLessThanOrElite(0.15)) > 2
                    ))
                    return true;
                if (CastSpellOnAdds("Incinerate"))
                    return true;
            }

            return false;
        }

        public override void Combat()
        {
            // Standard foo
            if (DoGlobalStuff())
                return;
            if (DoSomePetAndHealingStuff())
                return;
            if (CurrentBotName == "PvP" && CastFearIfFeasible())
                return;

            int burningEmbers = Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers);

            // MultiTarget Rotation
            if (Adds.Count > 0 && doMultitargetRotation(Adds.Count + 1))
                return;

            // No Multitarget, so please disable Fire and Brimstone.
            CastSelf("Fire and Brimstone", () => Me.HasAura("Fire and Brimstone"));

            // Priority #1
            if (Cast(
                    "Shadowburn", 
                    () => 
                           Target.HealthFraction <= 0.2
                    && (
                        Me.HasAura("Dark Soul: Instability")
                        || burningEmbers == 4 // No cast time so 4 is good enough!
                    )
                        
                ))
                return;

            // Priority #2
            if (CastPreventDouble(
                    "Immolate", 
                    () =>
                    !Target.HasAura("Immolate", true)
                    || Target.AuraTimeRemaining("Immolate") <= 4.5f
                ))
                return;

            // Priority #3
            if (Cast("Conflagrate"))
                return; // No Stack detection sadly... otherwise we would check for 2

            // Priority #4
            if (Cast(
                    "Chaos Bolt",
                    () =>
		            Me.HasAura("Dark Soul: Instability")
                    || burningEmbers >= 3
		        // because we don't know about .5 fractions of embers... sadly... But it fullifies the T16 4-piece boni
                ))
                return;

            // Priority #5
            // Refresh Immolate is already done in P#2

            // Priority #6
            // TODO: remember old cast position and check with target position and radius so we recast it when he gets out of the rain
            if (HasSpell("Aftermath") && CastOnTerrainPreventDouble("Rain of Fire", Target.Position, null, 7500))
                return;

            // Priority #7
            // Conflagrate is already done in #3
            // No Stack detection sadly... otherwise we would check for 1

            // Priority #8
            if (Cast("Incinerate"))
                return;
        }
    }
}
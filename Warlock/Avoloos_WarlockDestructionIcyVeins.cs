using System.Linq;
using ReBot.API;
using System;
using Avoloos.Warlock;
using Newtonsoft.Json;

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
        /// <summary>
        /// Health in % the target of havoc should have.
        /// </summary>
        [JsonProperty("DPS: Use Havoc on Mobs with HP in %")]
        public int HavocHealthPercentage = 40;

        /// <summary>
        /// Should havoc only be cast on focus / focus target if focus is friendly
        /// </summary>
        [JsonProperty("DPS: Use Havoc on your Focus (if friendly on its Target")]
        public bool UseHavocOnFocus = true;

        int ShadowBurnDamage {
            get {
                return (int) ( ( ( 315 / 100f ) * SpellPower ) * 1.24 );
            }
        }

        public AvoloosWarlockDestructionIcyVeins()
        {
            GroupBuffs = new[] {
                "Dark Intent",
                ( CurrentBotName == "PvP" ? "Create Soulwell" : null )
            };
            PullSpells = new[] {
                "Immolate",
                "Conflagrate",
                "Incinerate"
            };
            UsePet = true;
        }

        bool doMultitargetRotation(int mobsInFrontOfMe)
        {
            int burningEmbers = Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers);
           
            if (mobsInFrontOfMe >= 3)
                CastSelf("Mannoroth's Fury", () => HasSpell("Mannoroth's Fury") && !Me.HasAura("Mannoroth's Fury"));

            // Priority #1
            if (CastSpellOnBestAoETarget(
                    "Rain of Fire",
                    u => !HasAura("Rain of Fire") && ( HasAura("Mannoroth's Fury") || mobsInFrontOfMe >= 5 )
                ))
                return true;

            // Priority #2
            if (
                SpellCooldown("Havoc") <= 0.01 && burningEmbers >= 1 && mobsInFrontOfMe < 12) {
                // Dont waste Havoc apply it to one of the mid-enemies (high max health, low current health)
                var havocAdd = Me.Focus;

                if (!UseHavocOnFocus)
                    havocAdd = Adds
                        .OrderByDescending(x => x.Health)
                        .FirstOrDefault(x => x.HealthFraction <= HavocHealthPercentage / 100f && x.IsInLoS && x.DistanceSquared <= SpellMaxRangeSq("Havoc")) ?? Adds.FirstOrDefault();

                if (havocAdd != null && havocAdd.IsFriendly)
                    havocAdd = havocAdd.Target;

                if (havocAdd != null && Cast("Havoc", havocAdd))
                    return true;
            }

            // cast Chaosbolt or shadowburn on target as soon as possible and if feasible
            if (Adds.Count(x => x.HasAura("Havoc", true)) > 0 || burningEmbers >= 4) {
                var shadowBurnTarget = Adds
                    .Where(x => x.HealthFraction <= 0.249 && !x.HasAura("Havoc") && x.IsInLoS && x.DistanceSquared <= SpellMaxRangeSq("Shadowburn"))
                    .OrderByDescending(x => x.MaxHealth)
                    .FirstOrDefault() ?? Target;
                    
                if (Cast("Shadowburn", () => mobsInFrontOfMe < 12, shadowBurnTarget))
                    return true; 
                if (Cast("Chaos Bolt", () => mobsInFrontOfMe < 6))
                    return true;
            }

            if (mobsInFrontOfMe >= 3) {
                // Apply Immolate to all adds through Cataclysm
                if (CastSpellOnBestAoETarget("Cataclysm"))
                    return true;
            }

            // Priority #3
            var countAddsInRange = Adds.Count(x => x.DistanceSquaredTo(Target) <= SpellAoERange("Conflagrate"));
            if (( burningEmbers >= 2 && countAddsInRange > 2 )
                || ( burningEmbers >= 1 && countAddsInRange >= 8 )) {
                // Ensure Fire and Brimstone!
                CastSelf("Fire and Brimstone", () => !HasAura("Fire and Brimstone"));

                if (CastSpellOnBestAoETarget("Conflagrate"))
                    return true;
                if (CastSpellOnBestAoETarget("Immolate", y => !y.HasAura("Immolate") && y.HpLessThanOrElite(0.15)))
                    return true;
                if (CastSpellOnBestAoETarget("Incinerate"))
                    return true;
            }

            return false;
        }

        public override void Combat()
        {
            if (Me.IsCasting && Me.CastingSpellID == (int) WarlockSpellIds.CATACLYSM)
                return;

            if (DoSharedRotation())
                return;

            int burningEmbers = Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers);

            // MultiTarget Rotation
            if (Adds.Count > 0 && doMultitargetRotation(Adds.Count + 1))
                return;
	
			if (burningEmbers > 0 && Me.HealthFraction < 0.35)
				Cast("Ember Tap"); // instant

            // No Multitarget, so please disable Fire and Brimstone.
            CastSelf("Fire and Brimstone", () => Me.HasAura("Fire and Brimstone"));

            // Priority #1
            if (Cast(
                    "Shadowburn", 
                    () => 
                           Target.HealthFraction <= 0.2
                    && (
                        Me.HasAura("Dark Soul: Instability")
                        || burningEmbers >= 3// No cast time so 4 is good enough!
                        || Target.Health <= 2 * ShadowBurnDamage
                    )
                        
                ))
                return;

            // Priority #2
            if (CastPreventDouble(
                    "Immolate", 
                    () =>
                    !Target.HasAura("Immolate", true)
                    || ( Target.AuraTimeRemaining("Immolate") <= 3.5f && SpellCooldown("Cataclysm") > 1 )
                ))
                return;

            // Priority #3
            if (Cast("Conflagrate", () => SpellCharges("Conflagrate") >= 2))
                return;

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
            //if (CastSpellOnBestAoETarget("Rain of Fire", u => !HasAura("Rain of Fire")))
            //    return;

            // Priority #7
            if (Cast("Conflagrate", () => SpellCharges("Conflagrate") >= 2))
                return;

            // Refresh with cataclysm if possible
            if (CastSpellOnBestAoETarget("Cataclysm"))
                return;
                
            if (Cast("Conflagrate", () => SpellCharges("Conflagrate") == 1))
                return;

            // Priority #7
            if (Cast("Incinerate"))
                return;
        }
    }
}
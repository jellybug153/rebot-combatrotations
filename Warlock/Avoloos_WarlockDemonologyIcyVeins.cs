using System;
using ReBot.API;
using Avoloos.Warlock;
using ReBot.Helpers;
using System.Security.Cryptography.X509Certificates;
using Geometry;
using System.Linq;
using Mono.CSharp;

namespace ReBot
{
    enum DemonologySpellIDs
    {
        SHADOWBOLT = 686,
        TOUCHOFCAOS = 103964,
        CORRUPTION = 172,
        DOOM = 603,
    };

    [Rotation(
        "Warlock Demonology - Icy Veins Profile",
        "Avoloos",
        WoWClass.Warlock,
        Specialization.WarlockDemonology,
        40
    )] 
    public sealed class AvoloosWarlockDemonologyIcyVeins : WarlockBaseRotation
    {
        /// <summary>
        /// The hand of guldan spell lock.
        /// If this value is true Hand of Guldan will be cast, else it will not.
        /// </summary>
        bool handOfGuldanSpellLock = false;

        /// <summary>
        /// The minimum molten stacks for soulfire to be cast.
        /// </summary>
        int minMoltenStacksForSoulfire = 5;

        public AvoloosWarlockDemonologyIcyVeins()
        {
            GroupBuffs = new[] {
                "Dark Intent",
                ( CurrentBotName == "PvP" ? "Create Soulwell" : null )
            };
            PullSpells = new[] {
                "Shadow Bolt"
            };

            UsePet = true;
        }

        bool DoMultiTargetRotation(int mobsInFrontOfMe)
        {
            bool doSoulFire = true;
            bool doHellfire = false;
            bool doImmolationAura = false;

            //bool doChaosWave = false; // TODO: Support it for easy groups of enemies.
            bool dotAllTargets = false;
            
            if (mobsInFrontOfMe >= 6) {
                // Skip Soul Fire
                doSoulFire = false;
            } else if (mobsInFrontOfMe >= 4) {
                // TODO: Support Mannoroth's Fury
                doHellfire = !HasMetamorphosis;
                minMoltenStacksForSoulfire = 10;
            } else if (mobsInFrontOfMe >= 3) {
                doImmolationAura = HasMetamorphosis;
            } else {
                dotAllTargets = true;
            }
            
            if (dotAllTargets) { // Do all the Adds dotting.
                if (HasMetamorphosis) {
                    if (CastSpellOnAdds(
                            "Doom",
                            add => ( add.HpGreaterThanOrElite(0.15) && ( !add.HasAura("Doom") || add.AuraTimeRemaining("Doom") <= 18f ) )
                        ))
                        return true;
                } else {
                    if (CastSpellOnAdds(
                            "Corruption",
                            add => ( add.HpGreaterThanOrElite(0.15) && ( !add.HasAura("Corruption") || add.AuraTimeRemaining("Corruption") <= 6f ) )
                        ))
                        return true;
                }
            }

            if (Cast(
                    "Soul Fire",
                    () => doSoulFire
                    && ( Target.HealthFraction <= 0.25 )
                    || ( Me.HasAura("Molten Core", true, minMoltenStacksForSoulfire) )
                ))
                return true;
            
            // TODO: find a way to get close to the enemies (leap there?)
            if (doHellfire || doImmolationAura) {
                if (CastVariant("Hellfire", "Immolation Aura", Me))
                    return true;
            }
            
            // TODO: find a way to integrate Chaos Wave if targets are easy
            
            // Lets stick to our singleRotaiton if something above does not procc
            return false;
        }

        bool HasMetamorphosis {
            get {
                return Me.HasAura("Metamorphosis");
            }
        }

        bool DoMetamorphosis()
        {
            var currentFury = Me.GetPower(WoWPowerType.WarlockDemonicFury);

            if (HasMetamorphosis) {
                if (CastSelf(
                        "Metamorphosis",
                        () =>
                       ( currentFury < 750 && !Me.HasAura("Dark Soul: Knowledge") )
                        || ( SpellCooldown("Metamorphosis") <= 0.1 && !Me.InCombat )
                        || ( currentFury < 750 && ( Target.HasAura("Corruption", true) && Target.HasAura("Doom", true) ) && SpellCooldown("Metamorphosis") < 0 )
                    ))
                    return true;
            } else {
                // TODO: Tune the Dark Soul condition a bit...
                if (CastSelf(
                        "Metamorphosis",
                        () =>
                        Me.InCombat
                        && (
                            ( currentFury >= 850 )
                            || ( currentFury >= 400 && Me.HasAura("Dark Soul: Knowledge") )
                            || ( currentFury >= 200 && Target.HasAura("Corruption", true) && !Target.HasAura("Doom") )
                        ) 
                    ))
                    return true;
            }

            return false;
        }

        void ResetRotationVariables()
        {
            minMoltenStacksForSoulfire = 5;

            if (SpellCharges("Hand of Gul'dan") >= 2) {
                handOfGuldanSpellLock = true;
            }

            if (SpellCharges("Hand of Gul'dan") == 0) {
                handOfGuldanSpellLock = false;
            }
        }

        bool CastVariant(string spellNameNormal, string spellNameMetamorphosed, UnitObject target = null, Func<UnitObject, bool> unitCondition = null, Func<UnitObject, bool> normalCondition = null, Func<UnitObject, bool> metamorphedCondition = null)
        {
            if (target == null)
                target = Target;
            if (unitCondition == null)
                unitCondition = ( _ => true );
            if (normalCondition == null)
                normalCondition = ( _ => true );
            if (metamorphedCondition == null)
                metamorphedCondition = ( _ => true );

            Info("CastVariant got called: {0}, {1}", spellNameNormal, spellNameMetamorphosed);
            Info(
                "unitCondition: {0}, normalCondition/metamorphedCondition:{1}, Should Cast: {2}", 
                unitCondition(target),
                HasMetamorphosis ? metamorphedCondition(target) : normalCondition(target),
                HasMetamorphosis ? unitCondition(target) && metamorphedCondition(target) : unitCondition(target) && normalCondition(target)
            );
            return HasMetamorphosis ? Cast(
                spellNameMetamorphosed,
                () => unitCondition(target) && metamorphedCondition(target),
                target
            ) : Cast(
                spellNameNormal,
                () => unitCondition(target) && normalCondition(target),
                target
            );
        }

        bool CastVariant(DemonologySpellIDs spellIdNormal, DemonologySpellIDs spellIdMetamorphosed, UnitObject target = null, Func<UnitObject, bool> unitCondition = null, Func<UnitObject, bool> normalCondition = null, Func<UnitObject, bool> metamorphedCondition = null)
        {
            if (target == null)
                target = Target;
            if (unitCondition == null)
                unitCondition = ( _ => true );
            if (normalCondition == null)
                normalCondition = ( _ => true );
            if (metamorphedCondition == null)
                metamorphedCondition = ( _ => true );

            return HasMetamorphosis ? Cast(
                (int) spellIdMetamorphosed,
                () => unitCondition(target) && metamorphedCondition(target),
                target
            ) : Cast(
                (int) spellIdNormal,
                () => unitCondition(target) && normalCondition(target),
                target
            );
        }

        public override void Combat()
        {
            // reset some vars
            ResetRotationVariables();

            if (DoGlobalStuff())
                return;
            if (DoSomePetAndHealingStuff())
                return;
            //if (CastPreventDouble("Drain Life", () => Me.HealthFraction < 0.5, 10000)) return;

            if (CurrentBotName == "PvP" && CastFearIfFeasible())
                return;

            if (DoMetamorphosis())
                return;

            if (HasGlobalCooldown())
                return;

            // Always do Hand of Gul'dan id available and before tick ends
            // highest prio!
            if (CastSpellOnBestAoETarget(
                    "Hand of Gul'dan",
                    (u) => handOfGuldanSpellLock && u.AuraTimeRemaining("Hand of Gul'dan") <= 3f
                ))
                return;
            
            // Icy Veins Rotation
            if (Adds.Count > 0 && DoMultiTargetRotation(Adds.Count + 1))
                return;
                
            Info("Do Singlerotation");
            Info("Is Metamorphed: {0}", HasMetamorphosis);
            Info("No Hand of Gul'dan was cast");
            // Then we do the rest of our dots
            if (CastVariant(
                    "Corruption",
                    "Doom",
                    Target,
                    (u) => u.HpGreaterThanOrElite(0.15),
                    (u) => !u.HasAura("Corruption", true) || u.AuraTimeRemaining("Corruption") <= 4f,
                    (u) => !u.HasAura("Doom", true) || u.AuraTimeRemaining("Doom") <= 18f
                ))
                return;
            Info("No Corruption variant was cast");

            if (Cast(
                    "Soul Fire",
                    () => ( Me.HasAura("Molten Core", true) && Target.HealthFraction <= 0.25 )
                    || ( Me.HasAura("Molten Core", true, minMoltenStacksForSoulfire) )
                ))
                return;
            Info("No Soulfire was cast");

            // Fallback cast variant
            if (CastVariant("Shadow Bolt", "Touch of Chaos"))
                return;

            Info("No Shadowbolt variant was cast");
        }
    }
}
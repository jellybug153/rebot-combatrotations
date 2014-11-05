using System.ComponentModel;
using System.Linq;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;
using Avoloos.Warlock;

namespace ReBot
{
    [Rotation("Warlock Demonology - Icy Veins Profile", "Avoloos", WoWClass.Warlock, Specialization.WarlockDemonology, 40)]
    public sealed class Avoloos_WarlockDemonologyIcyVeins : WarlockBaseRotation
    {
        // If this value is true Hand of Guldan will be cast, else it will not.
        private bool handOfGuldanSpellLock = false;
        private int minMoltenStacksForSoulfire = 5;

        public Avoloos_WarlockDemonologyIcyVeins() : base()
        {
            GroupBuffs = new[]
            {
                "Dark Intent",
                (CurrentBotName == "PvP" ? "Create Soulwell" : null)
            };
            PullSpells = new[]
            {
                "Shadow Bolt"
            };

            UsePet = true;
        }

        private bool doMultiTargetRotation(int MobsInFrontOfMe)
        {
            bool doSoulFire = true;
            bool doHellfire = false;
            bool doImmolationAura = false;
            bool doShadowBolt = true;
            
            // Check for Felguard/Wrathguard
            bool doFellstorm = HasFelguard();
            bool doChaosWave = false; // TODO: Support it for easy groups of enemies.
            bool dotAllTargets = false;
            
            if(MobsInFrontOfMe >= 6) {
                // Skip Soul Fire
                doSoulFire = false;
            } else if(MobsInFrontOfMe >= 4) {
                // TODO: Support Mannoroth's Fury
                doHellfire = !Me.HasAura("Metamorphosis");
                doShadowBolt = false;
                minMoltenStacksForSoulfire = 10;
            } else if(MobsInFrontOfMe >= 3) {
                doImmolationAura = Me.HasAura("Metamorphosis");
            } else {
                doFellstorm = false; // I think it would be useless to kick the CD in for less than 3 mobs
                dotAllTargets = true;
            }
            
            // Always do Hand of Gul'dan id available and before tick ends
            // highest prio!
            // TODO: find a way to get the charges of Hands of Guldan and activate it only if 2 charges are there
            //       till we got no charges left, then deactivate till we got 2 again.
            handOfGuldanSpellLock = true;
            if (Cast(
                "Hand of Gul'dan",
                () => handOfGuldanSpellLock && Target.AuraTimeRemaining("Hand of Gul'dan") <= 3f
            )) return true;
            
            if(dotAllTargets)
            { // Do all the Adds dotting.
                if(Me.HasAura("Metamorphosis"))
                {
                    if(CastOnAdds(
                        "Doom",
                        (add) => (add.HpGreaterThanOrElite(0.15) && (!add.HasAura("Doom") || add.AuraTimeRemaining("Doom") <= 18f))
                    )) return true;
                }
                else
                {
                    if (CastOnAdds(
                        "Corruption",
                        (add) => (add.HpGreaterThanOrElite(0.15) && (!add.HasAura("Corruption") || add.AuraTimeRemaining("Corruption") <= 6f))
                    )) return true;
                }
            }

            if(Cast(
                "Soul Fire",
                () =>  
                   (Target.HealthFraction <= 0.25)
                || (Me.HasAura("Molten Core", true, minMoltenStacksForSoulfire))
            )) return true;
            
            // TODO: find a way to check if the Felguard is IN a group of adds...
            // But Hand of Gul'dan and Corruption should be more than enough time for the guard to get there.
            if(Cast(
                "Command Demon", 
                () => doFellstorm
            )) return true;
            
            // TODO: find a way to get close to the enemies (leap there?)

            if (CastPreventDouble(
                "Immolation Aura",
                () => doImmolationAura
            )) return true;
            if (CastPreventDouble(
               "Hellfire",
               () => doHellfire
           )) return true;
            
            // TODO: find a way to integrate Chaos Wave if targets are easy
            
            // Lets stick to our singleRotaiton if something above does not procc
            return false;
        }
        private bool doMetamorphosis()
        {
            var currentFury = Me.GetPower(WoWPowerType.WarlockDemonicFury);

            if (Me.HasAura("Metamorphosis"))
            {
                if (CastSelf(
                    "Metamorphosis",
                    () =>
                       (currentFury < 750 && !Me.HasAura("Dark Soul: Knowledge"))
                    || (SpellCooldown("Metamorphosis") == 0 && !Me.InCombat)
                )) return true;
            }
            else
            {
                // TODO: Tune the Dark Soul condition a bit...
                if (CastSelf(
                    "Metamorphosis",
                    () =>
                        Me.InCombat
                        && (
                            (currentFury >= 850)
                         || (currentFury >= 400 && Me.HasAura("Dark Soul: Knowledge"))
                        ) 
                )) return true;
            }

            return false;
        }
        
        public override void Combat()
        {
            // reset some vars
            minMoltenStacksForSoulfire = 5;

            if (doGlobalStuff()) return;
            if (doSomePetAndHealingStuff()) return;
            //if (CastPreventDouble("Drain Life", () => Me.HealthFraction < 0.5, 10000)) return;

            if (CurrentBotName == "PvP" && CastFearIfFeasible()) return;
            if (doMetamorphosis()) return;
            
            // Icy Veins Rotation
            if(Adds.Count > 0 && doMultiTargetRotation(Adds.Count + 1)) return;

            if (Me.HasAura("Metamorphosis"))
            {
                if (Cast(
                    "Doom",
                    () => (Target.HpGreaterThanOrElite(0.15) && (!Target.HasAura("Doom") || Target.AuraTimeRemaining("Doom") <= 18f))
                )) return;
            }
            else
            {
                if(Cast(
                    "Corruption",
                    () => (Target.HpGreaterThanOrElite(0.15) && (!Target.HasAura("Corruption") || Target.AuraTimeRemaining("Corruption") <= 6f))
                )) return;
            }

            // Always do Hand of Gul'dan id available and before tick ends
            // highest prio!
            // TODO: find a way to get the charges of Hands of Guldan and activate it only if 2 charges are there
            //       till we got no charges left, then deactivate till we got 2 again.
            handOfGuldanSpellLock = true;
            if (Cast(
                "Hand of Gul'dan",
                () => handOfGuldanSpellLock && Target.AuraTimeRemaining("Hand of Gul'dan") <= 3f
            )) return;

            if (Cast(
                "Soul Fire",
                () =>
                   (Target.HealthFraction <= 0.25)
                || (Me.HasAura("Molten Core", true, minMoltenStacksForSoulfire))
            )) return;

            if (Me.HasAura("Metamorphosis"))
            {
                if (Cast("Touch of Chaos")) return;
            }
            
            //if nothing was cast, then cast the good old shadow bolt
            Cast("Shadow Bolt");
        }
    }
}
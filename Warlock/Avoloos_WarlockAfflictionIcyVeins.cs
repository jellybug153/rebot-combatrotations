using System.Linq;
using Newtonsoft.Json;
using ReBot.API;
using Avoloos.Warlock;

namespace ReBot
{
    [Rotation(
        "Warlock Affliction - Icy Veins Profile",
        "Avoloos",
        WoWClass.Warlock,
        Specialization.WarlockAffliction,
        40
    )]
    public class AvoloosWarlockAfflictionIcyVeins : WarlockBaseRotation
    {
        [JsonProperty("Automatic mana-management through Life Tap")]
        public bool AutomaticManaManagement = true;

        public AvoloosWarlockAfflictionIcyVeins()
        {
            GroupBuffs = new[] {
                "Dark Intent",
                ( CurrentBotName == "PvP" ? "Create Soulwell" : null )
            };
            PullSpells = new[] {
                "Shadow Bolt"
            };
        }

        bool DoMultitargetRotation(int mobsInFrontOfMe)
        {
            if (
                mobsInFrontOfMe >= 3// Got a Group
                && HasFelguard()// and Has a Felguard
                && Cast("Command Demon") && HasGlobalCooldown())   // Lets rumble
                return true;

            /*
             * Against 5 or more enemies, you will need to start using Soulburn Icon Soulburn with Seed of Corruption Icon Seed of Corruption
             */
            if (mobsInFrontOfMe >= 5) {
                if (CastSpellOnAdds(
                        "Soulburn",
                        add => !Me.HasAura("Soulburn")
                    ))
                    return true;
            }
            if (Me.HasAura("Soulburn")) {
                if (CastSpellOnAdds(
                        "Seed of Corruption",
                        add => !add.HasAura("Seed of Corruption")
                    ))
                    return true;
            }

            /*
             * Against 2 enemies, use your normal rotation on one of them and keep your DoTs up on the other.
             * Against 3 or 4 enemies, keep your DoTs up and cast Drain Soul Icon Drain Soul. 
             * Against 5 or more enemies, While Seed of Corruption is ticking, you should maintain your DoTs on as many targets as possible.
             */
            foreach (var add1 in Adds.Where(x => x.IsInCombatRangeAndLoS)) {
                if (DoDotting(add1))
                    return true;
            }

            if (mobsInFrontOfMe >= 3) {
                /*
                 * Against 3 or 4 enemies, keep your DoTs up and cast Drain Soul Icon Drain Soul. 
                 */
                if (Cast("Drain Soul"))
                    return true;
            }

            return false;
        }

        public override void Combat()
        {
            if (DoGlobalStuff())
                return;
            if (DoSomePetAndHealingStuff())
                return;

            // Mana management
            if (AutomaticManaManagement && CastSelfPreventDouble(
                    "Life Tap",
                    () => Me.HealthFraction >= 0.65 && Me.Mana <= Me.Health * 0.16,
                    20
                ))
                return;

            //if (CurrentBotName == "PvP" && Cast("Drain Life", () => Me.HealthFraction <= 0.45 && Me.HasAura("Soulburn"))) return;
            if (CurrentBotName == "PvP" && CastFearIfFeasible())
                return;

            //Adds
            if (Adds.Count > 0) {
                if (CastOnTerrain(
                        "Shadowfury",
                        Target.Position,
                        () => Adds.Count(x => x.DistanceSquaredTo(Target) <= 12 * 12) > 2
                    ))
                    return;
                if (DoMultitargetRotation(Adds.Count + 1))
                    return;
            }

            // Single DPS
            if (DoDotting(Target))
                return;
            if (CastPreventDouble(
                    "Haunt", 
                    () => 
                 //   (Me.GetPower(WoWPowerType.WarlockSoulShards) >= 1 && Target.HpGreaterThanOrElite(0.1) && !Target.HasAura("Haunt"))
                 //|| ()
                    ( !Target.HasAura("Haunt") || Me.GetPower(WoWPowerType.WarlockSoulShards) >= 4 )
                    && (
                        // TODO: Trinket Procc
                        Target.HealthFraction <= 0.25f// the boss is reaching death
                        || Me.GetPower(WoWPowerType.WarlockSoulShards) > 3// We capped it (sadly we don't get it when we reached half a shard) (and yes I know whis will get this equation to true as we check against >= 4 above!)
                        || Me.HasAura("Dark Soul: Misery")
                    )
                ))
                return;

            // TODO: MultiDPS with Haunt, maybe?

            // Okay.. now souldrain :D
            if (Cast("Drain Soul"))
                return;
        }

        bool DoDotting(UnitObject u)
        {
            if (Cast(
                    "Agony",
                    () => Target.HpGreaterThanOrElite(0.3) && ( !Target.HasAura("Agony") || Target.AuraTimeRemaining("Unstable Affliction") <= 7f ),
                    u
                ))
                return true;
            if (Cast(
                    "Corruption",
                    () => Target.HpGreaterThanOrElite(0.15) && ( !Target.HasAura("Corruption") || Target.AuraTimeRemaining("Corruption") <= 5f ),
                    u
                ))
                return true;
            if (CastPreventDouble(
                    "Unstable Affliction",
                    () => Target.HpGreaterThanOrElite(0.2) && ( !Target.HasAura("Unstable Affliction") || Target.AuraTimeRemaining("Unstable Affliction") <= 5f ),
                    u
                ))
                return true;
            return false;
        }
    }
}
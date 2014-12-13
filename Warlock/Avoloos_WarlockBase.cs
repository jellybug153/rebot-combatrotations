using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReBot.API;
using WarlockCommon;
using System;
using System.Collections.Generic;
using Geometry;
using System.Runtime.InteropServices;

namespace Avoloos
{
    /// <summary>
    /// This class represents an Object, which can expire.
    /// </summary>
    public class ExpirableObject
    {
        /// <summary>
        /// The time where the <see cref="Avoloos.ExpirableObject"/> was created created.
        /// </summary>
        DateTime TimeCreated;

        /// <summary>
        /// Gets or sets the expires in milliseconds.
        /// </summary>
        /// <value>The expires in given milliseconds.</value>
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the expiring object.
        /// </summary>
        /// <value>The expiring object.</value>
        public object ExpiringObject { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Avoloos.ExpirableObject"/> class.
        /// </summary>
        /// <param name="expiringObject">The object which can expire.</param>
        /// <param name="expire">The time in milliseconds in which the given object will expire</param>
        public ExpirableObject(object expiringObject, int expire)
        {
            TimeCreated = DateTime.Now;
            ExpiringObject = expiringObject;
            ExpiresIn = expire;
        }

        /// <summary>
        /// Determines whether this instance is expired.
        /// </summary>
        /// <returns><c>true</c> if this instance is expired; otherwise, <c>false</c>.</returns>
        public bool IsExpired()
        {
            return DateTime.Now.Millisecond >= TimeCreated.Millisecond + ExpiresIn;
        }

        /// <summary>
        /// Will reset the expire timer, so the object will be again valid as set in the creation.
        /// </summary>
        public void ResetTime()
        {
            TimeCreated = DateTime.Now;
        }
    }
}
    
namespace Avoloos
{
    namespace Warlock
    {
        /// <summary>
        /// Warlock spell Ids
        /// </summary>
        public enum WarlockSpellIds
        {
            CATACLYSM = 152108
        }

        /// <summary>
        /// Warlock grimorie pets.
        /// </summary>
        public enum WarlockGrimoriePet
        {
            CurrentMainPet,
            SoulImp,
            Voidwalker,
            Succubus,
            Felhunter,
            Infernal,
            Doomguard,
        }

        /// <summary>
        /// Basic class which implements some convinience functions.
        /// </summary>
        abstract public class WarlockBaseRotation : CombatRotation
        {
            /// <summary>
            /// The selected pet.
            /// </summary>
            [JsonProperty("Pet: Selected Pet"), JsonConverter(typeof(StringEnumConverter))]
            public WarlockPet SelectedPet = WarlockPet.AutoSelect;

            /// <summary>
            /// Should use pet?
            /// </summary>
            [JsonProperty("Pet: Use Pet")]
            public bool UsePet = true;

            /// <summary>
            /// When should the warlock use HealtFunnel to heal the pet
            /// </summary>
            [JsonProperty("Pet: HealthFunnel Pet HP in %")]
            public int FunnelPetHp = 40;

            /// <summary>
            /// How much life does the player need to have to use Healtfunnel
            /// </summary>
            [JsonProperty("Pet: HealthFunnel Player HP in %")]
            public int FunnelPlayerHp = 90;

            /// <summary>
            /// Should the bot use Terrorguard/Infernal
            /// </summary>
            [JsonProperty("DPS: Use Terrorguard/Infernal/Grimorie of Service automatically")]
            public bool UseAdditionalDPSPet = true;

            /// <summary>
            /// Should the bot use dark Soul
            /// </summary>
            [JsonProperty("DPS: Use Dark Soul automatically")]
            public bool UseDarkSoul = true;

            /// <summary>
            /// Should Shadofury be used to intterupt?
            /// </summary>
            [JsonProperty("CC: Use Shadowfury as Interrupt")]
            public bool UseShadowfuryAsInterrupt = true;

            /// <summary>
            /// Should the Bot fear?
            /// </summary>
            [JsonProperty("PvP: Do Fear")]
            public bool FearDoFear = true;

            /// <summary>
            /// The fear ban time.
            /// </summary>
            [JsonProperty("PvP: Fear Ban Time")]
            public int FearBanTime = 10000;

            /// <summary>
            /// Should soulstone be used for the player if he is not in a group?
            /// </summary>
            [JsonProperty("Survival: Soulstone yourself if not in group")]
            public bool UseSelfSoulstone = true;

            /// <summary>
            /// Should the OOC-Rotation be disabled for the Fishingbot?
            /// </summary>
            [JsonProperty("General: Disable OutOfCombat for FishBot")]
            public bool DisableOutOfCombatFishbot = true;

            /// <summary>
            /// Should the bot use Life Tap if the Health is high and the mana is low?
            /// </summary>
            [JsonProperty("General: Automatic mana-management through Life Tap")]
            public bool AutomaticManaManagement = true;

            /// <summary>
            /// Defines the factor of HP a unit has to have to be counted as a boss.
            /// </summary>
            [JsonProperty("General/DPS: Percentual factor of a Targets MaxHP in relation to Players MaxHP to be valued as Bossencounter")]
            public int BossHealthPercentage = 200;

            /// <summary>
            /// Defines the +Level a Unit should have to be counted as a boss.
            /// </summary>
            [JsonProperty("General/DPS: +Level a Target has to have to be valued as Boss encounter")]
            public int BossLevelIncrease = 5;

            /// <summary>
            /// The selected pet.
            /// </summary>
            [JsonProperty("DPS: Grimorie of Service Pet"), JsonConverter(typeof(StringEnumConverter))]
            public WarlockGrimoriePet SelectedGrimoriePet = WarlockGrimoriePet.CurrentMainPet;

            /// <summary>
            /// The fear tracking list.
            /// </summary>
            protected List<ExpirableObject> FearTrackingList;

            /// <summary>
            /// Dictionary with all AoE effect ranges
            /// </summary>
            protected Dictionary<string, float> AoESpellRadius = new Dictionary<string, float> {
                { "Rain of Fire", 8 * 8 },
                { "Hand of Gul'dan", 6 * 6 },
                { "Felstorm", 8 * 8 },
                { "Shadowfury", 8 * 8 },
                { "Immolate", 10 * 10 }, // Fire and Brimstone Version
                { "Conflagrate", 10 * 10 }, // Fire and Brimstone Version
                { "Cataclysm", 8 * 8 }
            };

            /// <summary>
            /// Initializes a new instance of the <see cref="Avoloos.Warlock.WarlockBaseRotation"/> class.
            /// </summary>
            protected WarlockBaseRotation()
            {
                FearTrackingList = new List<ExpirableObject>();
            }

            /// <summary>
            /// Casts the given spell on the best target.
            /// If none is found it will always fallback to Target.
            /// </summary>
            /// <returns><c>true</c>, if spell on best target was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name.</param>
            /// <param name="castWhen">onlyCastWhen condition for Cast()</param>
            /// <param name="bestTargetCondition">Condition to limit the UnitObjects for a bestTarget</param>
            /// <param name="preventTime">Milliseconds in which the spell won't be cast again</param>
            /// <param name="targetOverride">Spell will be cast on this target</param>
            public bool CastSpellOnBestAoETarget(string spellName, Func<UnitObject, bool> castWhen = null, Func<UnitObject, bool> bestTargetCondition = null, int preventTime = 0, UnitObject targetOverride = null)
            {
                if (castWhen == null)
                    castWhen = (_) => true;

                if (bestTargetCondition == null)
                    bestTargetCondition = (_) => true;

                var aoeRange = SpellAoERange(spellName);
                var bestTarget = targetOverride ?? Adds
                    .Where(u => u.IsInCombatRangeAndLoS && u.DistanceSquared <= SpellMaxRangeSq(spellName) && bestTargetCondition(u))
                    .OrderByDescending(u => Adds.Count(o => Vector3.DistanceSquared(u.Position, o.Position) <= aoeRange)).FirstOrDefault() ?? Target;

                if (preventTime == 0) {
                    return SpellIsCastOnTerrain(spellName) ? CastOnTerrain(
                        spellName,
                        bestTarget.Position,
                        () => castWhen(bestTarget)
                    ) : Cast(
                        spellName,
                        bestTarget, 
                        () => castWhen(bestTarget)
                    );
                }

                return SpellIsCastOnTerrain(spellName) ? CastOnTerrainPreventDouble(
                    spellName,
                    bestTarget.Position,
                    () => castWhen(bestTarget),
                    preventTime
                ) : CastPreventDouble(
                    spellName,
                    () => castWhen(bestTarget),
                    bestTarget, 
                    preventTime
                );
            }

            public float SpellAoERange(string spellName)
            {
                var aoeRange = AoESpellRadius.FirstOrDefault(u => u.Key == spellName).Value;

                if ((int) aoeRange == 0)
                    aoeRange = 12 * 12; // Just guess the biggest one
                    
                if (HasAura("Mannoroth's Fury")) {
                    switch (spellName) {
                        case "Seed of Corruption":
                        case "Hellfire":
                        case "Immolation Aura":
                        case "Rain of Fire":
                            aoeRange *= 5;// 500%
                            break;
                    }
                }

                return aoeRange;
            }

            /// <summary>
            /// Determines whether the player has hand of guldan glyphed.
            /// </summary>
            /// <returns><c>true</c> if this hand of guldan is glyph; otherwise, <c>false</c>.</returns>
            public bool HasHandOfGuldanGlyph()
            {
                return API.LuaIf("for i = 1, NUM_GLYPH_SLOTS do local _,_,_,glyphSpellID,_ = GetGlyphSocketInfo(i); if(glyphSpellID == 56248) then return true end end return false");
            }

            /// <summary>
            /// Checks if the given Spell has to be cast on terrain.
            /// </summary>
            /// <returns><c>true</c>, if spell has to be cast on terrain, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name.</param>
            public bool SpellIsCastOnTerrain(string spellName)
            {
                switch (spellName) {
                    case "Shadowfury":
                        return true;
                    case "Rain of Fire":
                        return true;
                    case "Cataclysm":
                        return true;
                    case "Hand of Gul'dan":
                        return HasHandOfGuldanGlyph();
                    default:
                        return false;
                }
            }

            /// <summary>
            /// This will try to cast a fear spell. 
            /// 
            /// It will hereby utilize a FearBanList for players, so it won't try to fear all the time.
            /// </summary>
            /// <returns><c>true</c>, if a fear spell was cast, <c>false</c> otherwise.</returns>
            protected bool CastFearIfFeasible()
            {
                if (!FearDoFear)
                    return false;
                
                try {
                    FearTrackingList = FearTrackingList.Where(x => !x.IsExpired()).ToList(); // trim the list to all non expired feared objects.

                    var FearableAdds = Adds.Where(x => x.DistanceSquared < 11 * 11);
                    if (CastSelf("Howl of Terror", () => FearableAdds.Count() >= 2)) { 
                        foreach (var fearedAdd in FearableAdds.Where(x => x.IsPlayer)) {
                            FearTrackingList.Add(new ExpirableObject(fearedAdd, FearBanTime));
                        } // Do not fear them again (at least not with feat, howl of terror won't be affected by its fear descision, for the moment...)
                        return true;
                    }

                    foreach (var add in Adds.Where(x => x.IsPlayer && x.HasAura("Fear"))) {
                        // Add feared adds which were not feared by us
                        //if(FearTrackingList.Count(y => y.Unit == x) == 0)
                        var alreadyAdded = FearTrackingList.FirstOrDefault(y => add.Equals(y.ExpiringObject));
                        if (alreadyAdded != null) {
                            alreadyAdded.ResetTime();
                        } else {
                            FearTrackingList.Add(new ExpirableObject(add, FearBanTime));
                        }
                    }
                } catch (Exception e) { // catch everything
                    API.PrintError(
                        "Got an error in fear management logic. Disabling fear for now... Please Report to Avoloos.",
                        e
                    );
                    FearDoFear = false;
                }

                UnitObject add2 = Adds.FirstOrDefault(x => x.Target != null && x.DistanceSquared <= SpellMaxRangeSq("Fear"));
                if (add2 != null && add2.DistanceSquared < SpellMaxRangeSq("Fear")) {
                    var add = add2;
                    // Fear only real close targets which attack us
                    if (CastPreventDouble(
                            "Fear", () =>
                        ( !add.HasAura("Fear")// its not already feared by other ppl.
                        && !add.HasAura("Howl of Terror")// and its not already feared by howl
                        && Target.DistanceSquared <= 6.5 * 6.5// and its close
                        && add.Target == Me// and its targetting me
                        || add.IsCastingAndInterruptible() )// or its casting and we can interrupt it with fear :D
                        && ( FearDoFear && FearTrackingList.Count(x => add.Equals(x.ExpiringObject)) == 0 ) // and its not banned from fear
                        , add, 1000
                        ))
                        return true;
                }

                return false;
            }

            protected bool CastShadowfuryIfFeasible()
            {
                if (!UseShadowfuryAsInterrupt
                    && Adds.Count >= 3
                    && CastSpellOnBestAoETarget("Shadowfury"))
                    return true;

                if (UseShadowfuryAsInterrupt && CastSpellOnBestAoETarget(
                        "Shadowfury", 
                        (u) => u.IsCastingAndInterruptible(), 
                        (u) => u.IsCastingAndInterruptible()
                    ))
                    return true;

                return false;
            }

            /// <summary>
            /// Determines whether the current pet is a Felguard.
            /// </summary>
            /// <returns><c>true</c> if the current pet is a Felguard; otherwise, <c>false</c>.</returns>
            protected bool HasFelguard()
            {
                if (!Me.HasAlivePet)
                    return false;
                return ( WlPetDisplayId.Felguard.Equals(Me.Pet.DisplayId) || WlPetDisplayId.ImpFelguard.Equals(Me.Pet.DisplayId) );
            }

            /// <summary>
            /// Casts the spell on adds.
            /// </summary>
            /// <returns><c>true</c>, if spell on adds was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name.</param>
            protected bool CastSpellOnAdds(string spellName)
            {
                return CastSpellOnAdds(spellName, null);
            }

            /// <summary>
            /// Cast the spell on adds.
            /// </summary>
            /// <returns><c>true</c>, if spell on adds was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name.</param>
            /// <param name="castCondition">Condition which gets a UnitObject to decide if the spell may get cast on it.</param>
            protected bool CastSpellOnAdds(string spellName, Func<UnitObject, bool> castCondition)
            {
                castCondition = castCondition ?? ( add => true );

                foreach (var add in Adds) {
                    if (Cast(
                            spellName,
                            () => castCondition(add),
                            add
                        ))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Casts the spell on adds. Will prevent multiple casts.
            /// </summary>
            /// <returns><c>true</c>, if spell prevent double on adds was cast, <c>false</c> otherwise.</returns>
            /// <param name="spellName">Spell name to cast.</param>
            /// <param name="castCondition">Condition which gets a UnitObject to decide if the spell may get cast on it.</param>
            protected bool CastSpellPreventDoubleOnAdds(string spellName, Func<UnitObject, bool> castCondition)
            {
                castCondition = castCondition ?? ( add => true );

                foreach (var add in Adds) {
                    if (castCondition != null && CastPreventDouble(
                            spellName,
                            () => castCondition(add),
                            add
                        ))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Will be called when the bot is OutOfCombat.
            /// </summary>
            /// <returns><c>true</c>, if the function should be called again, <c>false</c> otherwise.</returns>
            public override bool OutOfCombat()
            {
                if (DisableOutOfCombatFishbot && ( CurrentBotName == "Fish" || CurrentBotName == "Auction" ))
                    return false;

                CastSelf("Metamorphosis", () => Me.HasAura("Metamorphosis"));
                CastSelf("Fire and Brimstone", () => Me.HasAura("Fire and Brimstone"));

                if (CastSelf("Dark Intent", () => !Me.HasAura("Dark Intent")))
                    return true;
                if (CastSelf("Unending Breath", () => Me.IsSwimming && !Me.HasAura("Unending Breath")))
                    return true;
                if (CastSelf(
                        "Soulstone",
                        () => UseSelfSoulstone
                        && CurrentBotName != "Combat"
                        && !Me.HasAura("Soulstone")
                        && API.LuaIf("GetNumGroupMembers() > 0")
                    ))
                    return true;


                if (SummonPet())
                    return true;

                return CastSelfPreventDouble("Create Healthstone", () => Inventory.Healthstone == null, 10000);
            }

            /// <summary>
            /// Summons the pet.
            /// </summary>
            /// <returns><c>true</c>, if pet was summoned, <c>false</c> otherwise.</returns>
            public bool SummonPet()
            {
                if (HasSpell("Grimoire of Sacrifice")) {
                    if (!HasAura("Grimoire of Sacrifice")) {
                        if (CastSelf(
                                "Flames of Xoroth",
                                () => !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1
                            ))
                            return true;

                        CastSelf(
                            "Soulburn",
                            () => !Me.HasAlivePet
                            && Me.GetPower(WoWPowerType.WarlockSoulShards) >= 1
                            && !HasAura("Soulburn")
                        );
                        if (this.SummonPet(SelectedPet))
                            return true;
                        if (CastSelf("Grimoire of Sacrifice", () => Me.HasAlivePet))
                            return true;
                    }
                } else if (UsePet) {
                    if (CastSelf(
                            "Flames of Xoroth",
                            () => !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1
                        ))
                        return true;
                    CastSelf(
                        "Soulburn",
                        () => !Me.HasAlivePet && Me.GetPower(WoWPowerType.WarlockSoulShards) >= 1
                    );
                    if (this.SummonPet(SelectedPet))
                        return true;
                } else if (Me.HasAlivePet) {
                    Me.PetDismiss();
                }

                return false;
            }

            /// <summary>
            /// Will be called when leaving combat
            /// </summary>
            /// <returns><c>true</c>, if the function should be called again, <c>false</c> otherwise.</returns>
            public override bool AfterCombat()
            {
                if (HasSpell("Fire and Brimstone") && CastSelf(
                        "Fire and Brimstone",
                        () => Me.HasAura("Fire and Brimstone")
                    ))
                    return true;
                if (CastSelf("Metamorphosis", () => Me.HasAura("Metamorphosis")))
                    return true;
                return false;
            }

            /// <summary>
            /// Checks if the given unit may be a boss unit.
            /// </summary>
            /// <returns><c>true</c>, if unit is (maybe) a boss, <c>false</c> otherwise.</returns>
            /// <param name="o">The Unit we want to check</param>
            public bool IsBoss(UnitObject o)
            {
                return ( o.IsElite() && o.MaxHealth * ( BossHealthPercentage / 100 ) >= Me.MaxHealth ) || o.Level >= Me.Level + BossLevelIncrease;
            }

            /// <summary>
            /// Do all the basic stuff which is shared among all specialisations.
            /// </summary>
            /// <returns><c>true</c>, if there was an GCD after a cast, <c>false</c> otherwise.</returns>
            protected bool DoSharedRotation()
            {
                if (UseDarkSoul) {
                    //no globalcd
                    bool DarkSoulCondition = ( Target.IsInCombatRangeAndLoS && Target.MaxHealth >= Me.MaxHealth && Target.IsElite() ) || IsBoss(Target);
                    CastSelfPreventDouble(
                        "Dark Soul: Instability",
                        () => DarkSoulCondition,
                        20000
                    );
                    CastSelfPreventDouble(
                        "Dark Soul: Knowledge",
                        () => DarkSoulCondition,
                        20000
                    );
                    CastSelfPreventDouble(
                        "Dark Soul: Misery",
                        () => DarkSoulCondition,
                        20000
                    );
                }

                Cast(
                    "Command Demon",
                    () => !HasSpell("Grimoire of Sacrifice") && ( this.IsPetActive("Summon Felhunter") || SelectedPet == WarlockPet.Felhunter ) && Target.IsCastingAndInterruptible()
                );
                Cast(
                    "Command Demon",
                    () => !HasSpell("Grimoire of Sacrifice") && ( this.IsPetActive("Summon Imp") || SelectedPet == WarlockPet.SoulImp ) && Me.HealthFraction <= 0.75
                );
                Cast(
                    "Command Demon",
                    () => !HasSpell("Grimoire of Sacrifice") && ( this.IsPetActive("Summon Voidwalker") || SelectedPet == WarlockPet.Voidwalker ) && Me.HealthFraction < 0.5
                );
                Cast(
                    "Command Demon",
                    () => !HasSpell("Grimoire of Sacrifice")
                    && HasFelguard()
                    && Adds.Count(u => Vector3.DistanceSquared(
                        Me.Pet.Position,
                        u.Position
                    ) <= 8 * 8) >= 2
                );
                Cast(
                    "Axe Toss",
                    () => !HasSpell("Grimoire of Sacrifice") && HasFelguard() && Target.IsCastingAndInterruptible()
                );

                //Heal
                CastSelf("Unbound Will", () => !Me.CanParticipateInCombat);
                CastSelf("Dark Bargain", () => Me.HealthFraction < 0.5);
                CastSelf("Sacrificial Pact", () => Me.HealthFraction < 0.6);
                CastSelf("Unending Resolve", () => Me.HealthFraction <= 0.5);
                CastSelf("Dark Regeneration", () => Me.HealthFraction <= 0.6);

                if (Me.HasAlivePet) {
                    UnitObject add = Adds.FirstOrDefault(x => x.Target == Me);
                    if (add != null)
                        Me.PetAttack(add);
                }

                if (Cast("Mortal Coil", () => Me.HealthFraction <= 0.5))
                    return true;

                if (SummonPet())
                    return true;

                // Disable DPS Pets if they are the "normal" one.
                if (!HasSpell("Demonic Servitude")) {
                    if (CastOnTerrain(
                            HasSpell("Grimoire of Supremacy") ? "Summon Abyssal" : "Summon Infernal",
                            Target.Position,
                            () => UseAdditionalDPSPet && ( ( ( Me.HealthFraction <= 0.75 || Target.HealthFraction <= 0.25 ) ) || Adds.Count >= 3 ) && IsBoss(Target)
                        ) || Cast(
                            HasSpell("Grimoire of Supremacy") ? "Summon Terrorguard" : "Summon Doomguard",
                            () => UseAdditionalDPSPet && ( Me.HealthFraction <= 0.5 || Target.HealthFraction <= 0.25 ) && IsBoss(Target)
                        ))
                        return true;
                }

                if (HasSpell("Grimorie: Imp")) {
                    bool GrimorieCondition = ( Target.MaxHealth >= Me.MaxHealth && Target.IsElite() ) || IsBoss(Target);
                    if (GrimorieCondition) {
                        var GrimoriePet = SelectedGrimoriePet;

                        if (GrimoriePet == WarlockGrimoriePet.CurrentMainPet) {
                            switch (SelectedPet) {
                                case WarlockPet.AutoSelect:
                                    if (Target.IsCastingAndInterruptible())
                                        GrimoriePet = WarlockGrimoriePet.Felhunter;
                                    else
                                        GrimoriePet = WarlockGrimoriePet.Doomguard;
                                    break;
                                case WarlockPet.SoulImp:
                                    GrimoriePet = WarlockGrimoriePet.SoulImp;
                                    break;
                                case WarlockPet.Voidwalker:
                                    GrimoriePet = WarlockGrimoriePet.Voidwalker;
                                    break;
                                case WarlockPet.Succubus:
                                    GrimoriePet = WarlockGrimoriePet.Succubus;
                                    break;
                                case WarlockPet.Felhunter:
                                    GrimoriePet = WarlockGrimoriePet.Felhunter;
                                    break;
                                case WarlockPet.Doomguard:
                                    GrimoriePet = WarlockGrimoriePet.Doomguard;
                                    break;
                                case WarlockPet.Infernal:
                                    GrimoriePet = WarlockGrimoriePet.Infernal;
                                    break;
                            }
                        }

                        switch (GrimoriePet) {
                            case WarlockGrimoriePet.SoulImp:
                                if (Cast("Grimorie: Imp"))
                                    return true;
                                break;
                            case WarlockGrimoriePet.Voidwalker:
                                if (Cast("Grimorie: Voidwalker"))
                                    return true;
                                break;
                            case WarlockGrimoriePet.Succubus:
                                if (Cast("Grimorie: Succubus"))
                                    return true;
                                break;
                            case WarlockGrimoriePet.Felhunter:
                                if (Cast("Grimorie: Felhunter"))
                                    return true;
                                break;
                            case WarlockGrimoriePet.Doomguard:
                                if (Cast("Grimorie: Doomguard"))
                                    return true;
                                break;
                            case WarlockGrimoriePet.Infernal:
                                if (Cast("Grimorie: Infernal"))
                                    return true;
                                break;
                        }
                    }
                }

                if (CastSelf(
                        "Ember Tap",
                        () => Me.HealthFraction <= 0.35 && Me.GetPower(WoWPowerType.WarlockDestructionBurningEmbers) >= 1
                    ))
                    return true;

                if (CastSelf(
                        "Health Funnel",
                        () => 
                    Me.HasAlivePet
                        && Me.Pet.HealthFraction <= ( FunnelPetHp / 100 )
                        && Me.HealthFraction >= ( FunnelPlayerHp / 100 )
                    ))
                    return true;
                    
                if (CurrentBotName == "PvP" && CastFearIfFeasible())
                    return true;

                if (CastShadowfuryIfFeasible())
                    return true;

                // Mana management
                if (HasSpell("Life Tap") && AutomaticManaManagement && CastSelfPreventDouble(
                        "Life Tap",
                        () => Me.HealthFraction >= 0.65 && Me.Mana <= Me.MaxHealth * 0.16,
                        20
                    ))
                    return true;
                    
                return HasGlobalCooldown();
            }
        }
    }
}

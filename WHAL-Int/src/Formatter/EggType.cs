using JsonCompilers;

namespace Formatter;

class EggType
{
    public static string ToDiscordEmoji(Egg eggType)
    {
        return eggType switch
        {
            Egg.Edible => "<:egg_edible:455467571613925418>",
            Egg.Superfood => "<:egg_superfood:455468082635210752>",
            Egg.Medical => "<:egg_medical:455468241582817299>",
            Egg.RocketFuel => "<:egg_rocketfuel:455468270661795850>",
            Egg.SuperMaterial => "<:egg_supermaterial:455468299480989696>",
            Egg.Fusion => "<:egg_fusion:455468334859681803>",
            Egg.Quantum => "<:egg_quantum:455468361099247617>",
            Egg.Crispr => "<:egg_immortality:455468394892886016>",
            Egg.Tachyon => "<:egg_tachyon:455468421048696843>",
            Egg.Graviton => "<:egg_graviton:455468444070969369>",
            Egg.Dilithium => "<:egg_dilithium:455468464639967242>",
            Egg.Prodigy => "<:egg_prodigy:455468487641661461>",
            Egg.Terraform => "<:egg_terraform:455468509099458561>",
            Egg.Antimatter => "<:egg_antimatter:455468542171807744>",
            Egg.DarkMatter => "<a:DarkMatterRules:890760221541142550>",
            Egg.Ai => "<:egg_ai:455468564590100490>",
            Egg.Nebula => "<:egg_nebula:455468583426981908>",
            Egg.Universe => "<:egg_universe:567345439381389312>",
            Egg.Enlightenment => "<:egg_enlightenment:844620906248929341>",
            Egg.Chocolate => "<:egg_chocolate:455470627663380480>",
            Egg.Easter => "<:egg_easter:455470644646379520>",
            Egg.Waterballoon => "<:egg_waterballoon:460976773430116362>",
            Egg.Firework => "<:egg_firework:460976588893454337>",
            Egg.Pumpkin => "<:egg_pumpkin:503686019896573962>",
            Egg.Unknown => "<:egg_unknown:455471603384582165>",

            _ => "<:egg_unknown:455471603384582165>"
        };
    }
}

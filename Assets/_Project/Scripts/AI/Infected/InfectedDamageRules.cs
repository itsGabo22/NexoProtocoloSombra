namespace Nexo.AI.Infected
{
    /// <summary>
    /// Números de daño de la M1911 y del cuchillo contra infectados (lógica pura, testeada en EditMode).
    /// - Cabeza: siempre letal para cualquier infectado (un disparo limpio).
    /// - Torso: 1 punto; un infectado normal aguanta 3 disparos al cuerpo y un reptante 2.
    /// - Piernas: 1 punto y cojera (la cojera la aplica SmartInfectedBrain).
    /// - Cuchillo: 2 puntos de frente; por la espalda sin que te vea, ejecución silenciosa (letal).
    /// </summary>
    public static class InfectedDamageRules
    {
        public const int StandardHealth = 3;
        public const int CrawlerHealth = 2;
        public const int BodyDamage = 1;
        public const int LegDamage = 1;
        public const int KnifeDamage = 2;

        /// <summary>Vida inicial según el perfil; un valor de override positivo manda.</summary>
        public static int MaxHealth(InfectedBehavior behavior, int overrideHealth)
        {
            if (overrideHealth > 0) return overrideHealth;
            return behavior == InfectedBehavior.Crawler ? CrawlerHealth : StandardHealth;
        }

        /// <summary>Daño de un disparo en la zona indicada con la vida que le queda al infectado.</summary>
        public static int ShotDamage(HitZone zone, int currentHealth)
        {
            switch (zone)
            {
                case HitZone.Head: return currentHealth > 0 ? currentHealth : 1;
                case HitZone.Legs: return LegDamage;
                default: return BodyDamage;
            }
        }

        /// <summary>Daño del cuchillo: letal en una ejecución sigilosa, 2 puntos en otro caso.</summary>
        public static int KnifeHit(bool stealthTakedown, int currentHealth)
        {
            if (stealthTakedown) return currentHealth > 0 ? currentHealth : 1;
            return KnifeDamage;
        }

        /// <summary>Disparos al cuerpo necesarios para abatir (comprobación de equilibrio).</summary>
        public static int BodyShotsToKill(InfectedBehavior behavior, int overrideHealth)
        {
            int health = MaxHealth(behavior, overrideHealth);
            return (health + BodyDamage - 1) / BodyDamage;
        }
    }
}

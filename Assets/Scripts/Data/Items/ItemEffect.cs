using UnityEngine;

namespace Data.Items
{
    /// <summary>
    /// Clase base abstracta para todos los efectos de ítems consumibles.
    /// Permite crear diferentes tipos de efectos de forma modular.
    /// </summary>
    public abstract class ItemEffect : ScriptableObject
    {
        [Header("Effect Info")]
        [SerializeField] protected string effectId;
        [SerializeField] protected string displayName;
        [TextArea(2, 4)]
        [SerializeField] protected string description;
        [SerializeField] protected Sprite effectIcon;

        /// <summary>
        /// ID único del efecto para referencia.
        /// </summary>
        public string EffectId => effectId;

        /// <summary>
        /// Nombre mostrado en interfaces.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Descripción del efecto.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Icono del efecto para UI.
        /// </summary>
        public Sprite EffectIcon => effectIcon;

        /// <summary>
        /// Ejecuta el efecto sobre el héroe especificado.
        /// </summary>
        /// <param name="hero">Héroe sobre el que aplicar el efecto</param>
        /// <param name="quantity">Cantidad/intensidad del efecto</param>
        /// <returns>True si el efecto se ejecutó correctamente</returns>
        public abstract bool Execute(HeroData hero, int quantity = 1);

        /// <summary>
        /// Obtiene una descripción del efecto que se aplicará.
        /// Útil para mostrar en tooltips antes de usar el ítem.
        /// </summary>
        /// <param name="quantity">Cantidad a aplicar</param>
        /// <returns>Texto descriptivo del efecto</returns>
        public abstract string GetPreviewText(int quantity = 1);

        /// <summary>
        /// Verifica si el efecto se puede ejecutar sobre el héroe especificado.
        /// </summary>
        /// <param name="hero">Héroe a verificar</param>
        /// <returns>True si el efecto se puede aplicar</returns>
        public virtual bool CanExecute(HeroData hero)
        {
            return hero != null;
        }

        /// <summary>
        /// Obtiene la prioridad de ejecución del efecto.
        /// Efectos con mayor prioridad se ejecutan primero.
        /// </summary>
        /// <returns>Valor de prioridad (mayor = más prioritario)</returns>
        public virtual int GetExecutionPriority()
        {
            return 0;
        }

        /// <summary>
        /// Método llamado después de ejecutar el efecto exitosamente.
        /// Útil para efectos adicionales o logging.
        /// </summary>
        /// <param name="hero">Héroe sobre el que se aplicó el efecto</param>
        /// <param name="quantity">Cantidad aplicada</param>
        protected virtual void OnEffectExecuted(HeroData hero, int quantity)
        {
            Debug.Log($"[ItemEffect] {DisplayName} executed on {hero.heroName} with quantity {quantity}");
        }

        /// <summary>
        /// Validación para asegurar que el efecto está configurado correctamente.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(effectId))
            {
                effectId = name;
            }
        }
    }
}

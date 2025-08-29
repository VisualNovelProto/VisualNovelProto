using UnityEngine;

namespace NaniPro.Core
{
    public class EnginePro : MonoBehaviour
    {
        [Header("Managers (auto-fill if null)")]
        public NaniPro.Managers.BackgroundManagerPro background;
        public NaniPro.Managers.CharacterManagerPro characters;
        public NaniPro.Managers.TextPrinterManagerPro textPrinter;
        public NaniPro.Managers.ChoiceHandlerManagerPro choiceHandler;
        public NaniPro.Managers.ScriptPlayerPro scriptPlayer;
        public NaniPro.Managers.VariableStore variables;
        public NaniPro.Managers.SaveManagerPro saveManager;

        private void Awake()
        {
            if (!background) background = gameObject.AddComponent<NaniPro.Managers.BackgroundManagerPro>();
            if (!characters) characters = gameObject.AddComponent<NaniPro.Managers.CharacterManagerPro>();
            if (!textPrinter) textPrinter = gameObject.AddComponent<NaniPro.Managers.TextPrinterManagerPro>();
            if (!choiceHandler) choiceHandler = gameObject.AddComponent<NaniPro.Managers.ChoiceHandlerManagerPro>();
            if (!scriptPlayer) scriptPlayer = gameObject.AddComponent<NaniPro.Managers.ScriptPlayerPro>();
            if (!variables) variables = gameObject.AddComponent<NaniPro.Managers.VariableStore>();
            if (!saveManager) saveManager = gameObject.AddComponent<NaniPro.Managers.SaveManagerPro>();

            // Inject
            scriptPlayer.backgrounds = background;
            scriptPlayer.characters = characters;
            scriptPlayer.printer = textPrinter;
            scriptPlayer.choices = choiceHandler;
            scriptPlayer.vars = variables;
            scriptPlayer.saver = saveManager;
        }
    }
}

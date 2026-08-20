using UnityEngine.UIElements;
using Sticki.UI.Core;

namespace Sticki.UI
{
    public class SimpleScreenController : UIScreenController
    {
        public string backButtonName;
        public System.Action OnBackClicked;

        protected override void OnInitialize()
        {
            if (!string.IsNullOrEmpty(backButtonName))
            {
                var btn = root.Q<Button>(backButtonName);
                if (btn != null) btn.clicked += () => OnBackClicked?.Invoke();
            }
        }
    }
}

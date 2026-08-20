using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace Sticki.UI.Core
{
    [RequireComponent(typeof(UIDocument))]
    public class UIRootController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private List<UIScreenController> _screens;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _screens = GetComponentsInChildren<UIScreenController>(true).ToList();
            
            foreach (var screen in _screens)
            {
                screen.Initialize(_uiDocument);
            }
        }

        public T GetScreen<T>() where T : UIScreenController
        {
            foreach (var screen in _screens)
            {
                if (screen is T tScreen) return tScreen;
            }
            return null;
        }

        public void ShowScreen<T>(bool hideOthers = false) where T : UIScreenController
        {
            foreach (var screen in _screens)
            {
                if (screen is T)
                {
                    screen.Show();
                }
                else if (hideOthers)
                {
                    screen.Hide();
                }
            }
        }

        public void HideScreen<T>() where T : UIScreenController
        {
            var screen = GetScreen<T>();
            if (screen != null) screen.Hide();
        }
    }
}

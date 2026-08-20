using UnityEngine;
using UnityEngine.UIElements;

namespace Sticki.UI.Core
{
    public abstract class UIScreenController : MonoBehaviour
    {
        [SerializeField] protected string rootName;
        [SerializeField] protected string visibleClassName = "visible";

        protected VisualElement root;
        protected UIDocument document;

        public VisualElement RootVisualElement => root;

        public virtual void Initialize(UIDocument doc)
        {
            document = doc;
            if (string.IsNullOrEmpty(rootName))
            {
                root = document.rootVisualElement;
            }
            else
            {
                root = document.rootVisualElement.Q<VisualElement>(rootName);
            }
            
            if (root == null)
            {
                Debug.LogWarning($"UIScreenController on {gameObject.name}: Root element '{rootName}' not found in UIDocument!");
            }
            else
            {
                // Ensure initial state is applied
                OnInitialize();
            }
        }

        protected virtual void OnInitialize() { }

        public virtual void Show()
        {
            if (root == null) return;
            
            root.style.display = DisplayStyle.Flex;
            if (!string.IsNullOrEmpty(visibleClassName))
            {
                root.AddToClassList(visibleClassName);
            }
            
            OnShow();
        }

        public virtual void Hide()
        {
            if (root == null) return;
            
            root.style.display = DisplayStyle.None;
            if (!string.IsNullOrEmpty(visibleClassName))
            {
                root.RemoveFromClassList(visibleClassName);
            }
            
            OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        
        public bool IsVisible()
        {
            if (root == null) return false;
            return root.style.display == DisplayStyle.Flex || 
                   (!string.IsNullOrEmpty(visibleClassName) && root.ClassListContains(visibleClassName));
        }
    }
}

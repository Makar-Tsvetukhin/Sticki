using UnityEngine;

namespace Sticki.Core.Interfaces
{
    public interface IInputSource
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        bool JumpPressed { get; }
        bool FireHeld { get; }
        bool ReloadPressed { get; }
        bool SprintHeld { get; }
        bool InspectPressed { get; }
        bool InteractPressed { get; }
        bool SelectArPressed { get; }
        bool SelectPistolPressed { get; }
        bool SelectLmgPressed { get; }
        bool SelectShotgunPressed { get; }
    }
}

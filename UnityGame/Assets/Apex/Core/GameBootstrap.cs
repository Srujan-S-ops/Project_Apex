using Apex.Race;
using Apex.UI;
using UnityEngine;

namespace Apex.Core
{
    public enum SceneRole { MainMenu, Race }

    /// <summary>
    /// The only component placed in scenes. Ensures services exist and spawns the scene director.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public SceneRole role = SceneRole.MainMenu;

        private void Awake()
        {
            GameServices.Ensure();
            switch (role)
            {
                case SceneRole.MainMenu:
                    gameObject.AddComponent<MenuDirector>();
                    break;
                case SceneRole.Race:
                    gameObject.AddComponent<RaceDirector>();
                    break;
            }
        }
    }
}

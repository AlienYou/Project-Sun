using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.UI;
using ProjectSun.FPS.Weapons;
using ProjectSun.FPS.World;
using UnityEngine;

namespace ProjectSun.FPS.Bootstrap
{
    /// <summary>
    /// Turns Unity's empty sample scene into a playable validation range. This has no scene dependency,
    /// so production scenes can be built separately while the feature scripts remain reusable.
    /// </summary>
    public sealed class FpsPrototypeBootstrap : MonoBehaviour
    {
        private static readonly Color FloorColor = new Color(0.06f, 0.08f, 0.11f);
        private static readonly Color WallColor = new Color(0.11f, 0.16f, 0.21f);
        private static readonly Color TargetColor = new Color(0.11f, 0.6f, 0.82f);

        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfMissing()
        {
            // A scene built with the Combat Slice builder owns its composition explicitly.
            if (FindObjectOfType<CombatSliceSceneInstaller>() != null) return;
            if (FindObjectOfType<FpsPrototypeBootstrap>() != null) return;
            new GameObject("FPS Prototype Bootstrap").AddComponent<FpsPrototypeBootstrap>();
        }

        private void Awake()
        {
            // Application.targetFrameRate = 120;
            // BuildRange();
        }

        private void BuildRange()
        {
            PrepareLighting();
            Camera playerCamera = CreatePlayer(out FpsPlayerController player, out HitscanWeapon weapon, out FpsAbilityController abilities, out Health health);
            CreateRangeGeometry();
            CreateTargets();

            FpsHud hud = gameObject.AddComponent<FpsHud>();
            hud.Configure(weapon, abilities, health);
            WeaponCustomizationUI customization = gameObject.AddComponent<WeaponCustomizationUI>();
            customization.Configure(weapon, player, abilities);
            playerCamera.gameObject.tag = "MainCamera";
        }

        private static Camera CreatePlayer(out FpsPlayerController player, out HitscanWeapon weapon, out FpsAbilityController abilities, out Health health)
        {
            foreach (Camera existingCamera in FindObjectsOfType<Camera>())
                existingCamera.enabled = false;

            GameObject playerObject = new GameObject("Player");
            playerObject.transform.position = new Vector3(0f, 0.03f, -14f);
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            health = playerObject.AddComponent<Health>();
            player = playerObject.AddComponent<FpsPlayerController>();

            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.fieldOfView = 78f;
            camera.clearFlags = CameraClearFlags.Skybox;
            player.Configure(cameraObject.transform, camera);

            Transform muzzle = CreateWeaponVisual(cameraObject.transform);
            weapon = playerObject.AddComponent<HitscanWeapon>();
            weapon.Configure(camera, muzzle);
            abilities = playerObject.AddComponent<FpsAbilityController>();
            abilities.Configure(player, weapon);
            WeaponFeedbackController feedback = playerObject.AddComponent<WeaponFeedbackController>();
            feedback.Configure(weapon, player, camera, muzzle.parent, muzzle);
            return camera;
        }

        private static Transform CreateWeaponVisual(Transform cameraTransform)
        {
            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weapon.name = "Prototype Carbine";
            weapon.transform.SetParent(cameraTransform, false);
            weapon.transform.localPosition = new Vector3(0.28f, -0.25f, 0.65f);
            weapon.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            weapon.transform.localScale = new Vector3(0.16f, 0.15f, 0.72f);
            Destroy(weapon.GetComponent<Collider>());
            ApplyColor(weapon, new Color(0.09f, 0.12f, 0.15f));

            GameObject muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(weapon.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            return muzzle.transform;
        }

        private static void CreateRangeGeometry()
        {
            CreateBlock("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(38f, 1f, 48f), FloorColor);
            CreateBlock("North Wall", new Vector3(0f, 3f, 20f), new Vector3(38f, 6f, 1f), WallColor);
            CreateBlock("South Wall", new Vector3(0f, 3f, -20f), new Vector3(38f, 6f, 1f), WallColor);
            CreateBlock("East Wall", new Vector3(19f, 3f, 0f), new Vector3(1f, 6f, 40f), WallColor);
            CreateBlock("West Wall", new Vector3(-19f, 3f, 0f), new Vector3(1f, 6f, 40f), WallColor);
            CreateBlock("Cover A", new Vector3(-5f, 1.1f, -2f), new Vector3(4f, 2.2f, 1.2f), WallColor);
            CreateBlock("Cover B", new Vector3(6f, 1.1f, 7f), new Vector3(3f, 2.2f, 1.2f), WallColor);
            CreateBlock("Cover C", new Vector3(-8f, 1.1f, 12f), new Vector3(2.5f, 2.2f, 1.2f), WallColor);
        }

        private static void CreateTargets()
        {
            Vector3[] positions =
            {
                new Vector3(-8f, 1.2f, -1f), new Vector3(5f, 1.2f, 2f), new Vector3(-2f, 1.2f, 9f),
                new Vector3(10f, 1.2f, 13f), new Vector3(-12f, 1.2f, 15f)
            };
            foreach (Vector3 position in positions)
            {
                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                target.name = "Training Target";
                target.transform.position = position;
                target.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
                ApplyColor(target, TargetColor);
                target.AddComponent<Health>();
                target.AddComponent<TargetDummy>();
            }
        }

        private static void PrepareLighting()
        {
            Light directional = FindObjectOfType<Light>();
            if (directional == null)
            {
                GameObject lightObject = new GameObject("Range Directional Light");
                directional = lightObject.AddComponent<Light>();
                directional.type = LightType.Directional;
                directional.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            }
            directional.intensity = 1.15f;
            RenderSettings.ambientIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.045f, 0.065f, 0.09f);
            RenderSettings.fogDensity = 0.009f;
        }

        private static void CreateBlock(string label, Vector3 position, Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = label;
            block.transform.position = position;
            block.transform.localScale = scale;
            ApplyColor(block, color);
        }

        private static void ApplyColor(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            renderer.material = material;
        }
    }
}

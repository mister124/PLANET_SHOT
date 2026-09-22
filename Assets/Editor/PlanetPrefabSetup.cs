#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DopamineSlayer.EditorTools
{
    /// <summary>Creates the editable runtime planet prefabs from the sprites the player sliced.</summary>
    public static class PlanetPrefabSetup
    {
        private const string SourceFolder = "Assets/Prefabs";
        private const string RuntimeFolder = "Assets/Resources/PlanetPrefabs";

        [MenuItem("Tools/Planet Shot/Prepare Editable Planet Prefabs")]
        public static void Prepare()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(RuntimeFolder);

            for (var tier = 0; tier < 8; tier++)
            {
                var name = $"PlanetSprites_{tier}";
                var sourcePath = $"{SourceFolder}/{name}.prefab";
                var targetPath = $"{RuntimeFolder}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) == null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) != null)
                {
                    var error = AssetDatabase.MoveAsset(sourcePath, targetPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"Could not move {name}: {error}");
                        continue;
                    }
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPath);
                if (prefab == null)
                {
                    Debug.LogError($"Missing prefab: {targetPath}");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(targetPath);
                root.name = name;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                var renderer = root.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.sortingOrder = 4;

                // A Ball always has a body.  Keeping it on the prefab prevents the
                // runtime launch object from ever being created without Rigidbody2D.
                var body = root.GetComponent<Rigidbody2D>();
                if (body == null) body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.simulated = false; // Preview balls are pictures, not physics bodies.
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                // Unity builds this collider from the sprite outline.  The artist can then
                // select this prefab and use Edit Collider to refine it in the Scene view.
                var wallCollider = root.GetComponent<PolygonCollider2D>();
                if (wallCollider == null) wallCollider = root.AddComponent<PolygonCollider2D>();
                wallCollider.isTrigger = false;

                if (tier == 7)
                {
                    var triggerTransform = root.transform.Find("Absorption Trigger");
                    if (triggerTransform == null)
                    {
                        var triggerObject = new GameObject("Absorption Trigger");
                        triggerObject.transform.SetParent(root.transform, false);
                        triggerTransform = triggerObject.transform;
                    }
                    var absorptionCollider = triggerTransform.GetComponent<PolygonCollider2D>();
                    if (absorptionCollider == null) absorptionCollider = triggerTransform.gameObject.AddComponent<PolygonCollider2D>();
                    absorptionCollider.isTrigger = true;
                    absorptionCollider.pathCount = wallCollider.pathCount;
                    for (var path = 0; path < wallCollider.pathCount; path++)
                        absorptionCollider.SetPath(path, wallCollider.GetPath(path));
                }

                PrefabUtility.SaveAsPrefabAsset(root, targetPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Planet prefabs are ready in Assets/Resources/PlanetPrefabs.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
#endif

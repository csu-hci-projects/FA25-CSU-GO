using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor helper to quickly add BallDustEffect to selected GameObjects
/// </summary>
public class BallDustEffectHelper : EditorWindow
{
    private GameObject dustPrefab;
    private float minSpeed = 0.5f;
    private float spawnInterval = 0.2f;
    private bool alignToGround = true;

    [MenuItem("Tools/Ball Dust Effect Helper")]
    public static void ShowWindow()
    {
        GetWindow<BallDustEffectHelper>("Ball Dust Helper");
    }

    void OnGUI()
    {
        GUILayout.Label("Add BallDustEffect to Selected Objects", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        dustPrefab = (GameObject)EditorGUILayout.ObjectField("Dust Prefab", dustPrefab, typeof(GameObject), false);
        minSpeed = EditorGUILayout.FloatField("Min Speed", minSpeed);
        spawnInterval = EditorGUILayout.FloatField("Spawn Interval", spawnInterval);
        alignToGround = EditorGUILayout.Toggle("Align To Ground", alignToGround);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Add to Selected GameObjects"))
        {
            AddToSelected();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "1. Select your ball prefabs in the Project window\n" +
            "2. Assign a dust particle prefab (from WarFX folder)\n" +
            "3. Click 'Add to Selected GameObjects'\n\n" +
            "This will add the BallDustEffect component with your settings.",
            MessageType.Info);
    }

    void AddToSelected()
    {
        if (dustPrefab == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Dust Prefab first!", "OK");
            return;
        }

        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please select at least one GameObject!", "OK");
            return;
        }

        int addedCount = 0;
        foreach (GameObject obj in selected)
        {
            // Check if it already has the component
            BallDustEffect existing = obj.GetComponent<BallDustEffect>();
            if (existing == null)
            {
                BallDustEffect effect = obj.AddComponent<BallDustEffect>();
                
                // Use reflection to set the serialized fields
                SerializedObject so = new SerializedObject(effect);
                so.FindProperty("dustPrefab").objectReferenceValue = dustPrefab;
                so.FindProperty("minSpeedForDust").floatValue = minSpeed;
                so.FindProperty("spawnInterval").floatValue = spawnInterval;
                so.FindProperty("alignToGround").boolValue = alignToGround;
                so.FindProperty("overrideLifetime").boolValue = true;
                so.FindProperty("destroyAfterSeconds").floatValue = 3f;
                so.ApplyModifiedProperties();
                
                addedCount++;
                Debug.Log($"Added BallDustEffect to {obj.name}");
            }
            else
            {
                Debug.Log($"{obj.name} already has BallDustEffect");
            }
        }

        EditorUtility.DisplayDialog("Success", $"Added BallDustEffect to {addedCount} GameObject(s)!", "OK");
        AssetDatabase.SaveAssets();
    }
}

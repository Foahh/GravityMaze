using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MazeGenerator.Core
{
    /// <summary>
    ///     Script for easily setting materials/textures on maze objects (walls, surfaces, etc.).
    ///     Attach this to the maze root or parent GameObject to apply materials to all matching objects.
    ///     Editor-only component for design-time material application.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class MaterialSetter : MonoBehaviour
    {
        [Header("Material Settings")]
        [Tooltip("Material to apply to all matching objects. If null, will not change existing materials.")]
        [SerializeField]
        private Material material;

        [Header("Matching Settings")] [Tooltip("Tag to match objects in the hierarchy.")] [SerializeField]
        private string objectTag = "";

        [Header("Search Settings")]
        [Tooltip("If true, will search recursively through all children. If false, only direct children.")]
        [SerializeField]
        private bool searchRecursively = true;

        [Tooltip("Maximum depth to search (0 = unlimited). Only applies when searching recursively.")] [SerializeField]
        private int maxSearchDepth;

        /// <summary>
        ///     Gets or sets the tag for matching objects.
        /// </summary>
        public string ObjectTag
        {
            get => objectTag;
            set => objectTag = value;
        }

        /// <summary>
        ///     Gets the number of objects found in the last search.
        /// </summary>
        public int ObjectCount => FindObjects().Count;

        /// <summary>
        ///     Applies the material to all matching objects in the hierarchy.
        /// </summary>
        public void ApplyMaterial()
        {
            if (material == null)
            {
                Debug.LogWarning("Material is not set. Please assign a material first.", this);
                return;
            }

            var objects = FindObjects();
            if (objects.Count == 0)
            {
                Debug.LogWarning("No objects found matching the current criteria.", this);
                return;
            }

            var appliedCount = 0;
            foreach (var renderer in objects)
            {
                if (renderer == null) continue;
                renderer.sharedMaterial = material;
                appliedCount++;
            }

            Debug.Log($"Applied material '{material.name}' to {appliedCount} object(s).", this);
        }

        /// <summary>
        ///     Finds all objects matching the current criteria.
        /// </summary>
        public List<Renderer> FindObjects()
        {
            var objects = new List<Renderer>();

            if (searchRecursively)
                FindObjectsRecursive(transform, objects, 0);
            else
                FindObjectsInChildren(transform, objects);

            return objects;
        }

        private void FindObjectsRecursive(Transform parent, List<Renderer> objects, int depth)
        {
            if (maxSearchDepth > 0 && depth >= maxSearchDepth) return;

            foreach (Transform child in parent)
            {
                if (IsMatch(child))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null) objects.Add(renderer);
                }

                FindObjectsRecursive(child, objects, depth + 1);
            }
        }

        private void FindObjectsInChildren(Transform parent, List<Renderer> objects)
        {
            foreach (Transform child in parent)
                if (IsMatch(child))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null) objects.Add(renderer);
                }
        }

        private bool IsMatch(Transform obj)
        {
            // Must have a Renderer component
            if (obj.GetComponent<Renderer>() == null) return false;

            // Match by tag
            return !string.IsNullOrEmpty(objectTag) && obj.CompareTag(objectTag);
        }

        /// <summary>
        ///     Sets a new material and applies it immediately.
        /// </summary>
        /// <param name="newMaterial">The material to apply</param>
        public void SetMaterial(Material newMaterial)
        {
            material = newMaterial;
            ApplyMaterial();
        }

        /// <summary>
        ///     Highlights all found objects in the scene view (Editor only).
        /// </summary>
        public void HighlightObjects()
        {
#if UNITY_EDITOR
            var objects = FindObjects();
            Selection.objects = objects.Select(r => r.gameObject).ToArray<Object>();
#else
            Debug.LogWarning("HighlightObjects is only available in the Unity Editor.", this);
#endif
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MaterialSetter))]
    [CanEditMultipleObjects]
    public class MaterialSetterEditor : Editor
    {
        private SerializedProperty materialProp;
        private SerializedProperty maxSearchDepthProp;
        private SerializedProperty objectTagProp;
        private SerializedProperty searchRecursivelyProp;

        private void OnEnable()
        {
            materialProp = serializedObject.FindProperty("material");
            objectTagProp = serializedObject.FindProperty("objectTag");
            searchRecursivelyProp = serializedObject.FindProperty("searchRecursively");
            maxSearchDepthProp = serializedObject.FindProperty("maxSearchDepth");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var setter = (MaterialSetter)target;

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(materialProp);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(objectTagProp);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(searchRecursivelyProp);

            EditorGUI.BeginDisabledGroup(!searchRecursivelyProp.boolValue);
            EditorGUILayout.PropertyField(maxSearchDepthProp);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Found Objects", setter.ObjectCount);
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply Material", GUILayout.Height(30)))
            {
                Undo.RecordObjects(GetAllRenderers(setter), "Apply Material");
                setter.ApplyMaterial();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Highlight Objects", GUILayout.Height(30))) setter.HighlightObjects();
            EditorGUILayout.EndHorizontal();

            // Show warning if material is not set
            if (materialProp.objectReferenceValue == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("No material assigned. Assign a material to apply it to objects.",
                    MessageType.Warning);
            }
        }

        private Object[] GetAllRenderers(MaterialSetter setter)
        {
            var objects = setter.FindObjects();
            return objects.Select(r => (Object)r).ToArray();
        }
    }
#endif
}
using System;
using MazeGenerator.Core;
using UnityEditor;
using UnityEngine;

namespace MazeGenerator.Editor
{
    public class MazeGeneratorWindow : EditorWindow
    {
        private readonly MazeGenerationSettings _settings = new();
        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("3D Maze Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _settings.mazeShape = (MazeShape)EditorGUILayout.EnumPopup("Shape", _settings.mazeShape);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
            _settings.gridSize = EditorGUILayout.IntSlider("Cells Per Side", _settings.gridSize, 2, 64);
            _settings.cellSize = EditorGUILayout.FloatField("Cell Size", _settings.cellSize);
            _settings.wallHeight = EditorGUILayout.FloatField("Wall Height", _settings.wallHeight);
            _settings.wallThickness = EditorGUILayout.FloatField("Wall Thickness", _settings.wallThickness);
            _settings.lidThickness = EditorGUILayout.FloatField("Lid Thickness", _settings.lidThickness);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
            _settings.wallMaterial =
                (Material)EditorGUILayout.ObjectField("Wall Material", _settings.wallMaterial, typeof(Material), false);
            _settings.groundMaterial = (Material)EditorGUILayout.ObjectField("Ground Material",
                _settings.groundMaterial, typeof(Material), false);
            _settings.lidMaterial = (Material)EditorGUILayout.ObjectField("Lid Material",
                _settings.lidMaterial, typeof(Material), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
            _settings.useRandomSeed = EditorGUILayout.Toggle("Use Random Seed", _settings.useRandomSeed);
            using (new EditorGUI.DisabledScope(_settings.useRandomSeed))
            {
                _settings.seed = EditorGUILayout.IntField("Seed", _settings.seed);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Maze")) GenerateMaze();
        }

        [MenuItem("Tools/Maze Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<MazeGeneratorWindow>();
            window.titleContent = new GUIContent("Maze Generator");
            window.minSize = new Vector2(320f, 240f);
        }

        private void GenerateMaze()
        {
            try
            {
                switch (_settings.mazeShape)
                {
                    case MazeShape.Disc:
                        MazeBuilder.CreateDiscMaze(_settings);
                        break;
                    case MazeShape.Cube:
                        MazeBuilder.CreateCubeMaze(_settings);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to generate maze: {ex.Message}");
            }
        }
    }
}
using System;
using UnityEngine;

namespace MazeGenerator.Core
{
    public enum MazeShape
    {
        Disc,
        Cube
    }

    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class DirectionHelper
    {
        public static readonly Direction[] AllDirections =
        {
            Direction.North,
            Direction.East,
            Direction.South,
            Direction.West
        };

        public static Direction Opposite(Direction direction)
        {
            return direction switch
            {
                Direction.North => Direction.South,
                Direction.South => Direction.North,
                Direction.East => Direction.West,
                Direction.West => Direction.East,
                _ => direction
            };
        }
    }

    [Serializable]
    public class MazeGenerationSettings
    {
        [Header("Maze Shape")]
        public MazeShape mazeShape = MazeShape.Disc;
        public int gridSize = 6;
        
        [Header("Dimensions")]
        public float cellSize = 1f;
        public float wallHeight = 1f;
        public float wallThickness = 0.15f;
        public float lidThickness = 0.1f;
        
        [Header("Difficulty")]
        [Range(0f, 1f)]
        [Tooltip("Percentage of dead-ends to remove (0 = keep all, 1 = remove all). Removing dead-ends creates more open areas.")]
        public float deadEndRemoval = 0f;
        
        [Range(0.5f, 1f)]
        [Tooltip("Goal distance as percentage of max possible path (0.5 = easy, 1.0 = hardest).")]
        public float goalDistanceRatio = 1f;
        
        [Header("Seed")]
        public bool useRandomSeed = true;
        public int seed;
        
        [Header("Materials")]
        public Material wallMaterial;
        public Material groundMaterial;
        public Material lidMaterial;
    }
}
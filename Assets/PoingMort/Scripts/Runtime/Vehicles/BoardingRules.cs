using System;
using UnityEngine;

namespace PoingMort.Vehicles
{
    public enum BoardingRefusal
    {
        None,
        TooFar,
        TooFast,
        SideBlocked,
        Occupied,
        Busy,
    }

    public enum ExitRefusal
    {
        None,
        TooFast,
        Blocked,
        Busy,
    }

    public enum VehicleSide { Left, Right }

    [Serializable]
    public class BoardingParameters
    {
        [Tooltip("Distance maximale entre le joueur et le point d'accès de la portière (m).")]
        public float maxDoorDistance = 1.7f;
        [Tooltip("Écart de vitesse maximal entre le joueur et la voiture (m/s).")]
        public float maxRelativeSpeed = 3.2f;
        [Tooltip("Distance à laquelle on peut héler une voiture (m).")]
        public float hailRange = 22f;
        [Tooltip("Allure que prend une voiture hélée (m/s). Elle ralentit mais ne s'arrête jamais.")]
        public float hailedSpeed = 4.2f;
        [Tooltip("Durée maximale du ralentissement après avoir hélé (s).")]
        public float hailDuration = 7f;
        [Tooltip("Durée de la transition d'embarquement (s).")]
        public float boardingDuration = 0.85f;

        [Tooltip("Vitesse au-delà de laquelle la sortie est refusée (m/s).")]
        public float maxExitSpeed = 14f;
        [Tooltip("Au-delà de cette vitesse, la sortie se termine par une chute/roulade (m/s).")]
        public float rollExitSpeed = 6.5f;
        [Tooltip("Durée de la sortie (s).")]
        public float exitDuration = 0.55f;
        [Tooltip("Délai avant de pouvoir retenter une sortie refusée (s).")]
        public float exitRetryDelay = 0.6f;
    }

    /// <summary>Pure decision rules for getting in and out of a moving car.</summary>
    public static class BoardingRules
    {
        public static BoardingRefusal CanBoard(float distanceToDoor, float relativeSpeed, bool sideFree, bool occupied, bool playerBusy, BoardingParameters p)
        {
            if (playerBusy) return BoardingRefusal.Busy;
            if (occupied) return BoardingRefusal.Occupied;
            if (distanceToDoor > p.maxDoorDistance) return BoardingRefusal.TooFar;
            if (relativeSpeed > p.maxRelativeSpeed) return BoardingRefusal.TooFast;
            if (!sideFree) return BoardingRefusal.SideBlocked;
            return BoardingRefusal.None;
        }

        /// <summary>Chooses the exit side. The preferred side (usually the kerb side) is tried first.</summary>
        public static ExitRefusal CanExit(float vehicleSpeed, bool preferredFree, bool otherFree, VehicleSide preferred, bool busy, BoardingParameters p, out VehicleSide side)
        {
            side = preferred;
            if (busy) return ExitRefusal.Busy;
            if (vehicleSpeed > p.maxExitSpeed) return ExitRefusal.TooFast;
            if (preferredFree) return ExitRefusal.None;
            if (otherFree)
            {
                side = preferred == VehicleSide.Left ? VehicleSide.Right : VehicleSide.Left;
                return ExitRefusal.None;
            }
            return ExitRefusal.Blocked;
        }

        public static bool ExitEndsInRoll(float vehicleSpeed, BoardingParameters p) => vehicleSpeed > p.rollExitSpeed;

        /// <summary>Smooth boarding path in vehicle-local space: start → door → seat, with a small hop.</summary>
        public static Vector3 BoardingPath(Vector3 startLocal, Vector3 doorLocal, Vector3 seatLocal, float t)
        {
            t = Mathf.Clamp01(t);
            const float split = 0.55f;
            if (t < split)
            {
                float u = t / split;
                float s = u * u * (3f - 2f * u);
                Vector3 p = Vector3.Lerp(startLocal, doorLocal, s);
                p.y += Mathf.Sin(u * Mathf.PI) * 0.12f;
                return p;
            }
            else
            {
                float u = (t - split) / (1f - split);
                float s = u * u * (3f - 2f * u);
                return Vector3.Lerp(doorLocal, seatLocal, s);
            }
        }

        public static Vector3 ExitPath(Vector3 seatLocal, Vector3 doorLocal, Vector3 exitLocal, float t)
        {
            t = Mathf.Clamp01(t);
            const float split = 0.45f;
            if (t < split)
            {
                float u = t / split;
                float s = u * u * (3f - 2f * u);
                return Vector3.Lerp(seatLocal, doorLocal, s);
            }
            else
            {
                float u = (t - split) / (1f - split);
                float s = u * u * (3f - 2f * u);
                Vector3 p = Vector3.Lerp(doorLocal, exitLocal, s);
                p.y += Mathf.Sin(u * Mathf.PI) * 0.1f;
                return p;
            }
        }

        public static string Describe(BoardingRefusal r)
        {
            switch (r)
            {
                case BoardingRefusal.TooFar: return "Rapproche-toi de la portière";
                case BoardingRefusal.TooFast: return "Trop rapide : cours à sa vitesse";
                case BoardingRefusal.SideBlocked: return "Portière inaccessible";
                case BoardingRefusal.Occupied: return "Voiture déjà occupée";
                case BoardingRefusal.Busy: return "Impossible maintenant";
                default: return string.Empty;
            }
        }

        public static string Describe(ExitRefusal r)
        {
            switch (r)
            {
                case ExitRefusal.TooFast: return "Trop rapide pour sauter : ralentis";
                case ExitRefusal.Blocked: return "Sortie bloquée des deux côtés";
                case ExitRefusal.Busy: return "Impossible maintenant";
                default: return string.Empty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.World
{
    public class WaxCandle : MonoBehaviour
    {
        const float ShortestGap = 0.1f;

        const float LongestFallShown = 30f;

        const float SpoutMarkerRadius = 0.15f;

        static readonly Color SpoutColour = new Color(1f, 0.78f, 0.35f);

        static readonly List<RaycastHit2D> Hits = new List<RaycastHit2D>();

        [Header("Dripping")]
        [Tooltip("The wax drop this candle drips: a prefab with a small trigger collider, a " +
                 "Kinematic Rigidbody 2D, a Wax Drop and its art. Leave it empty and the " +
                 "candle drips nothing and says so in the console.")]
        public WaxDrop dropPrefab;

        [Tooltip("Seconds from one drop forming to the next. Each drop hangs as a warning and " +
                 "then falls, so the stretch between one drop landing and the next one letting " +
                 "go is the window to get past underneath. The Haste spell slows the rhythm " +
                 "along with everything else.")]
        [Min(ShortestGap)] public float secondsBetweenDrops = 2.5f;

        [Tooltip("Where in its rhythm this candle starts, as a fraction of the gap between " +
                 "drops. A row of candles all left at 0 drip together, which is one obstacle; " +
                 "set them to 0, 0.33 and 0.66 and they drip in turn, which is a corridor you " +
                 "have to time.")]
        [Range(0f, 1f)] public float phase = 0f;

        [Tooltip("Where drops form, relative to the candle - under the lip of the candle or its " +
                 "holder. For a candle standing on a ledge, move it out past the edge: a drop " +
                 "that forms inside the floor splashes the moment it lets go. Select the candle " +
                 "to see the spot and a line down to where its drops land.")]
        public Vector2 spoutOffset = new Vector2(0f, -0.5f);

        [NonSerialized] float clock;

        float Interval => Mathf.Max(ShortestGap, secondsBetweenDrops);

        Vector3 Spout => transform.position + (Vector3)spoutOffset;

        void Awake()
        {
            clock = phase * Interval;

            if (dropPrefab == null)
                Debug.LogWarning($"{name}: WaxCandle has no Drop Prefab, so it never drips. " +
                                 "Drag the wax drop prefab into it.", this);
        }

        void Update()
        {
            clock += Haste.DeltaTime;

            if (clock < Interval)
                return;

            clock = Mathf.Repeat(clock, Interval);
            Drip();
        }

        void Drip()
        {
            if (dropPrefab != null)
                Instantiate(dropPrefab, Spout, Quaternion.identity);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 spout = Spout;
            float reach = dropPrefab != null ? dropPrefab.maxFallDistance : LongestFallShown;
            Vector3 landing = spout + Vector3.down * reach;

            ContactFilter2D ground = Layers.MakeSolidOnlyFilter(PickGroundLayers());

            if (Physics2D.Raycast(spout, Vector2.down, ground, Hits, reach) > 0)
                landing.y = Hits[0].point.y;

            Gizmos.color = SpoutColour;
            Gizmos.DrawWireSphere(spout, SpoutMarkerRadius);
            Gizmos.DrawLine(spout, landing);
        }

        LayerMask PickGroundLayers()
        {
            LayerMask ground = Layers.GroundMask;

            if (dropPrefab != null)
                ground = dropPrefab.groundLayers;

            return ground;
        }
    }
}

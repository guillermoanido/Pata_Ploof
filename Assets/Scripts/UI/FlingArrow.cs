using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.UI
{
    public class FlingArrow : MonoBehaviour
    {
        const float HeadTurn = 45f;
        const float DiagonalToSide = 0.707f;

        const float FaintestAlpha = 0.45f;

        Sprite art;
        float thickness;
        float headSize;
        Color safe;
        Color danger;
        int order;

        SpriteRenderer shaft;
        SpriteRenderer head;

        public static FlingArrow Make(Sprite art, float thickness, float headSize, Color safe,
            Color danger, int sortingOrder)
        {
            var go = new GameObject("Fling Arrow");
            var arrow = go.AddComponent<FlingArrow>();

            arrow.art = art;
            arrow.thickness = Mathf.Max(0.01f, thickness);
            arrow.headSize = Mathf.Max(0.01f, headSize);
            arrow.safe = safe;
            arrow.danger = danger;
            arrow.order = sortingOrder;

            arrow.shaft = arrow.MakePiece("Shaft");
            arrow.head = arrow.MakePiece("Head");

            return arrow;
        }

        public void Hide()
        {
            if (shaft != null)
                shaft.enabled = false;

            if (head != null)
                head.enabled = false;
        }

        public void Show(Vector2 from, Vector2 direction, float length, bool hazard, float charge)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon || length <= 0f)
            {
                Hide();
                return;
            }

            Vector2 way = direction.normalized;
            float turn = Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg;

            Color tint = hazard ? danger : safe;
            tint.a *= Mathf.Lerp(FaintestAlpha, 1f, Mathf.Clamp01(charge));

            if (art != null)
            {
                head.enabled = false;
                Draw(shaft, from + way * (length * 0.5f), turn, new Vector2(length, headSize), tint);
                return;
            }

            float stem = Mathf.Max(0f, length - headSize);

            Draw(shaft, from + way * (stem * 0.5f), turn, new Vector2(stem, thickness), tint);

            Draw(head, from + way * (stem + headSize * 0.5f), turn + HeadTurn,
                Vector2.one * (headSize * DiagonalToSide), tint);
        }

        static void Draw(SpriteRenderer piece, Vector2 at, float turn, Vector2 size, Color tint)
        {
            Vector2 unit = piece.sprite != null ? (Vector2)piece.sprite.bounds.size : Vector2.one;

            if (unit.x <= Mathf.Epsilon || unit.y <= Mathf.Epsilon)
                unit = Vector2.one;

            piece.enabled = true;
            piece.color = tint;
            piece.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, turn));
            piece.transform.localScale = new Vector3(size.x / unit.x, size.y / unit.y, 1f);
        }

        SpriteRenderer MakePiece(string named)
        {
            var host = new GameObject(named);
            host.transform.SetParent(transform, false);

            var piece = host.AddComponent<SpriteRenderer>();
            piece.sprite = art != null ? art : Placeholder.Box;
            piece.sortingOrder = order;
            piece.enabled = false;

            return piece;
        }
    }
}

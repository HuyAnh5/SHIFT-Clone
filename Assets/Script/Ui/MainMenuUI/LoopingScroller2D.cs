using UnityEngine;

public class LoopingScroller2D : MonoBehaviour
{
    [SerializeField] float speed = 3f;
    [SerializeField] Transform[] pieces;   // 2 ground pieces
    [SerializeField] float pieceWidth = 20f; // chiều rộng world-unit của 1 piece

    void Reset()
    {
        // auto fill if possible
        pieces = new Transform[transform.childCount];
        for (int i = 0; i < pieces.Length; i++) pieces[i] = transform.GetChild(i);
    }

    void Update()
    {
        float dx = speed * Time.deltaTime;

        for (int i = 0; i < pieces.Length; i++)
            pieces[i].position += Vector3.left * dx;

        // wrap: tìm piece nào chạy quá trái thì đẩy nó ra sau piece phải nhất
        float leftMostX = float.MaxValue;
        float rightMostX = float.MinValue;
        int leftMostIndex = 0;

        for (int i = 0; i < pieces.Length; i++)
        {
            float x = pieces[i].position.x;
            if (x < leftMostX) { leftMostX = x; leftMostIndex = i; }
            if (x > rightMostX) rightMostX = x;
        }

        // nếu piece trái nhất đã đi quá -pieceWidth thì đẩy nó ra sau
        if (leftMostX <= rightMostX - pieceWidth - 0.1f)
        {
            pieces[leftMostIndex].position = new Vector3(rightMostX + pieceWidth, pieces[leftMostIndex].position.y, pieces[leftMostIndex].position.z);
        }
    }
}

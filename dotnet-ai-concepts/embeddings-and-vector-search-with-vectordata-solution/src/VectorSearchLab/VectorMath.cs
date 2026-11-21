using System.Numerics.Tensors;

namespace VectorSearchLab;

public static class VectorMath
{
    public static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException($"Vectors must have the same length ({a.Length} vs {b.Length}).");
        }

        float normA = TensorPrimitives.Norm(a);
        float normB = TensorPrimitives.Norm(b);
        return normA == 0 || normB == 0 ? 0 : TensorPrimitives.Dot(a, b) / (normA * normB);
    }

    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        float norm = TensorPrimitives.Norm(vector);
        var result = vector.ToArray();
        if (norm > 0)
        {
            TensorPrimitives.Divide(result, norm, result);
        }

        return result;
    }
}

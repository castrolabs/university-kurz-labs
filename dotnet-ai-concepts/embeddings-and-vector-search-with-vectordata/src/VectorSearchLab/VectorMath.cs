namespace VectorSearchLab;

public static class VectorMath
{
    public static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        // TODO-00: Cosine similarity: dot(a, b) / (|a| * |b|). Throw ArgumentException when the lengths
        //          differ and return 0 when either vector has no length (never NaN).
        //          System.Numerics.Tensors.TensorPrimitives (Dot, Norm) does the arithmetic.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static float[] Normalize(ReadOnlySpan<float> vector)
    {
        // TODO-01: Return a NEW array with the same direction and length 1 (divide by the norm).
        //          A zero vector comes back as zeros. Do not modify the input.
        throw new NotImplementedException("Not implemented yet.");
    }
}

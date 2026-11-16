using System;
using System.Collections.Generic;

/// <summary>화면과 네트워크에 의존하지 않는 3x3 슬라이딩 퍼즐 규칙입니다.</summary>
public sealed class SlidingPuzzleModel
{
    public const int Width = 3;
    public const int CellCount = Width * Width;
    public const int MinimumSolutionMoves = 8;
    public const int MaximumSolutionMoves = 10;

    // 최초 생성 시 한 번만 탐색하고 모든 로컬 퍼즐에서 후보를 공유합니다.
    private static readonly Lazy<int[][]> _startingStates = new Lazy<int[][]>(BuildStartingStates);

    private readonly int[] _cells = new int[CellCount];
    private int _emptyIndex;

    public bool IsSolved
    {
        get
        {
            for (int i = 0; i < CellCount - 1; i++)
            {
                if (_cells[i] != i + 1)
                    return false;
            }
            return _cells[CellCount - 1] == 0;
        }
    }

    public SlidingPuzzleModel()
    {
        ResetSolved();
    }

    public int[] GetStateCopy()
    {
        return (int[])_cells.Clone();
    }

    public int GetTileAt(int index)
    {
        if (index < 0 || index >= CellCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _cells[index];
    }

    public bool CanMove(int tileId)
    {
        int index = GetTileIndex(tileId);
        if (index < 0)
            return false;

        int rowDistance = Math.Abs(index / Width - _emptyIndex / Width);
        int columnDistance = Math.Abs(index % Width - _emptyIndex % Width);
        return rowDistance + columnDistance == 1;
    }

    public bool TryMove(int tileId)
    {
        if (IsSolved || !CanMove(tileId))
            return false;

        SwapWithEmpty(GetTileIndex(tileId));
        return true;
    }

    /// <summary>검증에 실패하면 현재 상태를 변경하지 않습니다.</summary>
    public bool TryApplyState(int[] state)
    {
        if (!IsValidState(state))
            return false;

        Array.Copy(state, _cells, CellCount);
        _emptyIndex = Array.IndexOf(_cells, 0);
        return true;
    }

    public void Shuffle(Random random)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        int[][] candidates = _startingStates.Value;
        int[] selected = candidates[random.Next(candidates.Length)];
        Array.Copy(selected, _cells, CellCount);
        _emptyIndex = Array.IndexOf(_cells, 0);
    }

    private static int[][] BuildStartingStates()
    {
        int[] solved = { 1, 2, 3, 4, 5, 6, 7, 8, 0 };
        Queue<(int[] cells, int emptyIndex, int distance)> pending =
            new Queue<(int[], int, int)>();
        HashSet<long> visited = new HashSet<long>();
        List<int[]> candidates = new List<int[]>();
        pending.Enqueue((solved, CellCount - 1, 0));
        visited.Add(EncodeState(solved));

        // 너비 우선 탐색: 처음 발견한 거리가 정답까지의 최소 이동 횟수입니다.
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current.distance >= MinimumSolutionMoves)
                candidates.Add(current.cells);
            if (current.distance == MaximumSolutionMoves)
                continue;

            for (int index = 0; index < CellCount; index++)
            {
                int rowDistance = Math.Abs(index / Width - current.emptyIndex / Width);
                int columnDistance = Math.Abs(index % Width - current.emptyIndex % Width);
                if (rowDistance + columnDistance != 1)
                    continue;

                int[] next = (int[])current.cells.Clone();
                next[current.emptyIndex] = next[index];
                next[index] = 0;
                if (visited.Add(EncodeState(next)))
                    pending.Enqueue((next, index, current.distance + 1));
            }
        }
        return candidates.ToArray();
    }

    private static long EncodeState(int[] state)
    {
        long key = 0;
        for (int i = 0; i < CellCount; i++)
            key = key * 9 + state[i];
        return key;
    }

    private void ResetSolved()
    {
        for (int i = 0; i < CellCount - 1; i++)
            _cells[i] = i + 1;
        _emptyIndex = CellCount - 1;
        _cells[_emptyIndex] = 0;
    }

    private int GetTileIndex(int tileId)
    {
        if (tileId < 1 || tileId >= CellCount)
            return -1;
        return Array.IndexOf(_cells, tileId);
    }

    private void SwapWithEmpty(int index)
    {
        _cells[_emptyIndex] = _cells[index];
        _cells[index] = 0;
        _emptyIndex = index;
    }

    private static bool IsValidState(int[] state)
    {
        if (state == null || state.Length != CellCount)
            return false;

        int seen = 0;
        int inversions = 0;
        for (int i = 0; i < CellCount; i++)
        {
            int value = state[i];
            if (value < 0 || value >= CellCount || (seen & (1 << value)) != 0)
                return false;

            seen |= 1 << value;
            if (value == 0)
                continue;

            for (int j = i + 1; j < CellCount; j++)
            {
                if (state[j] != 0 && state[j] < value)
                    inversions++;
            }
        }

        // 가로가 홀수인 보드는 빈칸을 제외한 역순 쌍의 개수가 짝수여야 풀 수 있습니다.
        return inversions % 2 == 0;
    }
}

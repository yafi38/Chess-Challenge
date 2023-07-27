using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
  private const int infinity = 999999;
  private readonly int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

  private Move? lastIterationBestMove = null;
  private Move? bestMove = null;
  private bool iterationHalted = false;
  private int totalEvaluations = 0;
  //private TranspositionTable transpositionTable = new(64);

  public Move Think(Board board, Timer timer)
  {
    Move[] moves = board.GetLegalMoves();

    const int maxDepth = 15;
    iterationHalted = false;
    lastIterationBestMove = null;
    var maxScore = 0;
    int depth = 1;

    for (depth = 1; depth < maxDepth; depth++)
    {
      bestMove = null;
      //transpositionTable.Clear();
      var score = MiniMax(board, timer, depth, -infinity, infinity, isRoot: true);
      //Console.WriteLine("Depth: {0}, Evals: {1}", depth, totalEvaluations);
      //totalEvaluations = 0;

      if (!iterationHalted)
      {
        lastIterationBestMove = bestMove;
        maxScore = score;
        //Console.WriteLine("Depth: {0}, Score: {1}, {2}", depth, score, lastIterationBestMove.Value.ToString());
      }
      else
      {
        break;
      }

      if (score == infinity || score == -infinity) break;
    }

    //Console.WriteLine("Evals: {0}", totalEvaluations);
    //totalEvaluations = 0;
    Console.WriteLine();

    return lastIterationBestMove ?? moves[0];
  }

  public int MiniMax(Board board, Timer timer, int remainingDepth, int alpha, int beta, bool isRoot = false)
  {
    if (remainingDepth == 0)
    {
      totalEvaluations++;
      return QuiescenceSearch(board, alpha, beta);
    }
    if (timer.MillisecondsElapsedThisTurn > 1000)
    {
      iterationHalted = true;
      return 0;
    }

    Move[] moves = GetOrderedMoves(board);

    foreach (var move in moves)
    {
      int eval;

      board.MakeMove(move);

      /*if (transpositionTable.TryRetrieve(board.ZobristKey, remainingDepth, out var storedEval))
      {
        eval = storedEval;
      }
      else*/
      {
        if (board.IsInCheckmate())
        {
          eval = infinity;
        }
        else if (board.IsDraw())
        {
          eval = 0;
        }
        else
        {
          eval = -MiniMax(board, timer, remainingDepth - 1, -beta, -alpha);
        }
        //if (!iterationHalted) transpositionTable.Store(board.ZobristKey, eval, remainingDepth);
      }
      
      board.UndoMove(move);

      if (iterationHalted) break;

      if (eval >= beta)
      {
        if (eval == beta && isRoot) bestMove = move;
        return beta;
      }

      if (eval > alpha)
      {
        alpha = eval;
        if (isRoot) bestMove = move;
      }
    }

    return alpha;
  }

  private int StaticEvaluation(Board board)
  {
    var pieceLists = board.GetAllPieceLists();
    int score = 0;

    foreach (var pieceList in pieceLists)
    {
      score += (pieceList.IsWhitePieceList ? 1 : -1) * GetPieceValue(pieceList.TypeOfPieceInList) * pieceList.Count;
    }

    bool[] isWhites = { true, false };

    foreach (var isWhite in isWhites)
    {
      if (board.HasKingsideCastleRight(isWhite))
      {
        score += isWhite ? 5 : -5;
      }
      if (board.HasQueensideCastleRight(isWhite))
      {
        score += isWhite ? 5 : -5;
      }
    }

    return board.IsWhiteToMove ? score : -score;
  }

  private int QuiescenceSearch(Board board, int alpha, int beta)
  {
    var eval = StaticEvaluation(board);

    if (eval >= beta) return beta;
    if (eval > alpha) alpha = eval;
    
    Move[] moves = board.GetLegalMoves(true);

    foreach (var move in moves)
    {
      board.MakeMove(move);
      eval = -QuiescenceSearch(board, -beta, -alpha);
      board.UndoMove(move);

      if (eval >= beta) return beta;
      if (eval > alpha) alpha = eval;
    }

    return alpha;
  }


  private Move[] GetOrderedMoves(Board board)
  {
    var moves = board.GetLegalMoves();
    int[] scores = new int[moves.Length];

    for (int i = 0; i < moves.Length; i++)
    {
      var move = moves[i];
      var score = 0;
      
      if (move.IsCapture)
      {
        //Console.WriteLine($"Found capture {move}");
        score += 10 * GetPieceValue(move.CapturePieceType) - GetPieceValue(move.MovePieceType);
      }

      if (move.IsPromotion)
      {
        score += GetPieceValue(move.PromotionPieceType);
      }


      scores[i] = score;
    }

    Array.Sort(scores, moves);

    Array.Reverse(moves);
    //Array.Reverse(scores);

    //for (int i = 0; i < moves.Length; i++)
    //{
    //  Console.WriteLine($"{moves[i]}, Score: {scores[i]}");
    //}
    //Console.WriteLine();
    return moves;
  }

  private int GetPieceValue(PieceType pieceType)
  {
    return pieceValues[(int)pieceType];
  }
}

/*public class TranspositionTable
{
  public Dictionary<ulong, LinkedListNode<TranspositionTableEntry>> table;
  private LinkedList<TranspositionTableEntry> lruList;
  public int maxCapacity;
  public int hit = 0;
  public int miss = 0;

  public TranspositionTable(int maxCapacityInMB)
  {
    int maxCapacityInBytes = maxCapacityInMB * 1024 * 1024;
    maxCapacity = maxCapacityInBytes / (3 * sizeof(ulong) + 2 * sizeof(int));
    table = new Dictionary<ulong, LinkedListNode<TranspositionTableEntry>>(maxCapacity);
    lruList = new LinkedList<TranspositionTableEntry>();
    //Console.WriteLine("Max Capacity {0}", maxCapacity);
  }

  public void Store(ulong zobristKey, int evaluation, int depth)
  {
    if (table.ContainsKey(zobristKey))
    {
      var lruNode = table[zobristKey];
      table.Remove(zobristKey);
      lruList.Remove(lruNode);
    }

    if (table.Count >= maxCapacity)
    {
      table.Remove(lruList.Last.Value.ZobristKey);
      lruList.RemoveLast();
    }

    var entry = lruList.AddFirst(new TranspositionTableEntry(zobristKey, evaluation, depth));
    table[zobristKey] = entry;
  }

  public bool TryRetrieve(ulong zobristKey, int depth, out int evaluation)
  {
    if (table.TryGetValue(zobristKey, out var entry))
    {
      if (entry.Value.Depth >= depth)
      {
        lruList.Remove(entry);
        lruList.AddFirst(entry);

        evaluation = entry.Value.Evaluation;
        hit++;
        return true;
      }
    }

    evaluation = 0;
    miss++;
    return false;
  }

  public void Clear()
  {
    table.Clear();
    lruList.Clear();
  }
}

public class TranspositionTableEntry
{
  public ulong ZobristKey;
  public int Evaluation;
  public int Depth;

  public TranspositionTableEntry(ulong zobristKey, int evaluation, int depth)
  {
    ZobristKey = zobristKey;
    Evaluation = evaluation;
    Depth = depth;
  }
}*/
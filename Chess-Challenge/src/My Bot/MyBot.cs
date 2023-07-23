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
  private TranspositionTable transpositionTable = new(64);

  public Move Think(Board board, Timer timer)
  {
    Move[] moves = board.GetLegalMoves();

    const int maxDepth = 10;
    iterationHalted = false;
    bestMove = null;
    lastIterationBestMove = null;

    for (int depth = 1; depth < maxDepth; depth++)
    {
      transpositionTable.Clear();
      var score = MiniMax(board, timer, depth, -infinity, infinity, isRoot: true); ;

      Console.WriteLine("Depth: {0}, Evals: {1}", depth, totalEvaluations);
      totalEvaluations = 0;

      if (!iterationHalted)
      {
        lastIterationBestMove = bestMove;
        Console.WriteLine("Depth: {0}, Score: {1}, {2}", depth, score, lastIterationBestMove.Value.ToString());
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
      if (IsCheckMateAfterMove(board, move))
      {
        if (isRoot) bestMove = move;
        return infinity;
      }

      int eval;
      if (IsDrawAfterMove(board, move))
      {
        eval = 0;
      }
      else
      {
        board.MakeMove(move);

        if (!transpositionTable.TryRetrieve(board.ZobristKey, out eval))
        {
          eval = -MiniMax(board, timer, remainingDepth - 1, -beta, -alpha);
          transpositionTable.Store(board.ZobristKey, eval);
        }

        board.UndoMove(move);
      }

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

  private bool IsCheckMateAfterMove(Board board, Move move)
  {
    board.MakeMove(move);
    bool isMate = board.IsInCheckmate();
    board.UndoMove(move);
    return isMate;
  }

  private bool IsDrawAfterMove(Board board, Move move)
  {
    board.MakeMove(move);
    bool isDraw = board.IsDraw();
    board.UndoMove(move);
    return isDraw;
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

      if (IsDrawAfterMove(board, move))
      {
        score -= 10000;
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

public class TranspositionTable
{
  private Dictionary<ulong, LinkedListNode<Tuple<ulong, int>>> table;
  private LinkedList<Tuple<ulong, int>> lruList;
  private int maxCapacity;
  public int hit = 0;
  public int miss = 0;

  public TranspositionTable(int maxCapacityInMB)
  {
    int maxCapacityInBytes = maxCapacityInMB * 1024 * 1024;
    maxCapacity = maxCapacityInBytes / (3 * sizeof(ulong) + 2 * sizeof(int));
    table = new Dictionary<ulong, LinkedListNode<Tuple<ulong, int>>>(maxCapacity);
    lruList = new LinkedList<Tuple<ulong, int>>();
    Console.WriteLine("Max Capacity {0}", maxCapacity);
  }

  public void Store(ulong zobristKey, int evaluation)
  {
    if (table.ContainsKey(zobristKey))
    {
      var lruNode = table[zobristKey];
      table.Remove(zobristKey);
      lruList.Remove(lruNode);
    }

    if (table.Count >= maxCapacity)
    {
      table.Remove(lruList.Last.Value.Item1);
      lruList.RemoveLast();
    }

    var entry = lruList.AddFirst(Tuple.Create(zobristKey, evaluation));
    table[zobristKey] = entry;
  }

  public bool TryRetrieve(ulong zobristKey, out int evaluation)
  {
    if (table.TryGetValue(zobristKey, out var entry))
    {
      lruList.Remove(entry);
      lruList.AddFirst(entry); // Move the key to the front to indicate recent usage
      evaluation = entry.Value.Item2;
      hit++;
      return true;
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
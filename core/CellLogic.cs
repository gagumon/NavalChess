using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace NavalChess.Models
{
	public enum TerrainType { Sea, Island, none }
	public enum UnitType
	{
		carrier, battleship, cruiser, destroyer, submarine, mine, radar, turret
	}
	public enum GameState
	{
		Menu,
		Setup,
		PlayerDeploy,
		CombatPhase,
		Finish
	}
	public enum Sequence
	{
		Row,
		Col,
	}
	public readonly record struct CellPosition
	{
		public int Row { get; init; }
		public int Col { get; init; }
		public CellPosition(int row, int col)
		{
			Row = row;
			Col = col;
		}
	}
	public class CellInfo
	{
		public TerrainType Terrain { get; set; } = TerrainType.Sea;
		public bool IsOccupied => (OccupiedBy != null);
		public bool IsCheck { get; set; } = false;
		public Ship? OccupiedBy { get; set; } = null;
		public int DectNum { get; set; } = 0;
		public bool IsDetect { get; set; } = false;

	}
	public class BoardConfig
	{
		public int Rows { get; set; } = 12;
		public int Columns { get; set; } = 8;
		public static int CellSize { get; set; } = 50;
		public int InitialBudget { get; set; } = 500;

		public BoardConfig() { }

		public BoardConfig(int rows, int cols, int cellSize, int initialBudget)
		{
			Rows = rows;
			Columns = cols;
			CellSize = cellSize;
			InitialBudget = initialBudget;
		}
		public static Dictionary<CellPosition, CellInfo> BuildMirroredBoard(BoardConfig config, Dictionary<CellPosition, CellInfo> leftHalf)
		{
			int rows = config.Rows;
			int halfCols = config.Columns;

			var fullBoard = new Dictionary<CellPosition, CellInfo>();

			for (int row = 0; row < rows; row++)
			{
				for (int col = 0; col < halfCols; col++)
				{
					var pos = new CellPosition(row, col);
					if (!leftHalf.ContainsKey(pos)) continue;

					var baseCell = leftHalf[pos];

					// 左側
					fullBoard[pos] = new CellInfo
					{
						Terrain = baseCell.Terrain,
						IsCheck = false,
						IsDetect = false,
						DectNum = 0,
					};

					// 右側鏡射
					var mirrorCol = halfCols * 2 - 1 - col;
					var mirroredPos = new CellPosition(row, mirrorCol);

					fullBoard[mirroredPos] = new CellInfo
					{
						Terrain = baseCell.Terrain,
						IsCheck = false,
						IsDetect = false,
						DectNum = 0,
					};
				}
			}
			//標籤
			for (int row = 0; row < rows; row++)
			{
				var leftLabelPos = new CellPosition(row, -1);
				var rightLabelPos = new CellPosition(row, halfCols * 2);
				fullBoard[leftLabelPos] = new CellInfo { DectNum = 0, IsDetect = false, Terrain = TerrainType.none };
				fullBoard[rightLabelPos] = new CellInfo { DectNum = 0, IsDetect = false, Terrain = TerrainType.none };
			}
			for (int col = 0; col < halfCols * 2; col++)
			{
				var bottomLabelPos = new CellPosition(rows, col);
				fullBoard[bottomLabelPos] = new CellInfo { DectNum = 0, IsDetect = false, Terrain = TerrainType.none };
			}

			return fullBoard;
		}
		public static Dictionary<CellPosition, CellInfo> BuildEmptyBoard(
			int rows,
			int cols,
			Dictionary<CellPosition, CellInfo>? existing = null)
		{
			var board = new Dictionary<CellPosition, CellInfo>();
			var defaultTerrain = TerrainType.Sea;

			for (int r = 0; r < rows; r++)
			{
				for (int c = 0; c < cols; c++)
				{
					var pos = new CellPosition(r, c);

					if (existing != null && existing.TryGetValue(pos, out var existingCell))
					{
						board[pos] = new CellInfo
						{
							Terrain = existingCell.Terrain,
							IsCheck = false,
						};
					}
					else
					{
						board[pos] = new CellInfo
						{
							Terrain = defaultTerrain,
							IsCheck = false,
						};
					}
				}
			}
			return board;
		}
	}
	public class ComplexData
	{
		public List<Player> Players { get; }
		public BoardConfig Board { get; }
		public Dictionary<CellPosition, CellInfo> CellInfo { get; }
		public ComplexData(List<Player> players, BoardConfig board, Dictionary<CellPosition, CellInfo> cellinfo)
		{
			Players = players;
			Board = board;
			CellInfo = cellinfo;
		}
	}
	public class Player
	{
		public string Name { get; private set; } = "";
		public int Budget { get; set; }
		public List<Ship> Ships { get; init; } = [];

		private Player() { }

		public static Player Create(string name, int budget)
		{
			return new Player
			{
				Name = name,
				Budget = budget
			};
		}
	}
	public static class UnitTypeHelper
	{
		public static readonly HashSet<UnitType> ShipTypes = new()
	{
		UnitType.carrier,
		UnitType.battleship,
		UnitType.cruiser,
		UnitType.destroyer,
		UnitType.submarine
	};
		public static bool IsShip(UnitType id) => ShipTypes.Contains(id);
	}

	public class UnitTypeInfo
	{
		public required string Name { get; init; }
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required string UImagePath { get; init; }
		public required string UnitImagePath { get; init; }
		public required int Price { get; init; }
		public required UnitType Id { get; init; }
		public string? Description { get; init; }
		public required TerrainType Terrain { get; init; }
		public string SizeText => $"{Width}×{Height}格";
		public bool IsShip => UnitTypeHelper.IsShip(Id);
		public string? CursorPath { get; init; }
		public int CursorRange { get; init; }
	}
	public class Cursor
	{
		public UnitType Id { get; set; }
		public string? Path { get; set; }
		public int Range { get; set; }

		public static Cursor FromUnitInfo(UnitTypeInfo template)
		{
			return new Cursor
			{
				Id = template.Id,
				Range = template.CursorRange,
				Path = template.UnitImagePath != null ? template.CursorPath : "",
			};
		}
		public HashSet<CellPosition> GetRangeGrid(CellPosition pos, int angle)
		{
			var Grid = new HashSet<CellPosition>();
			int i = 1; int j = 1;
			if (angle == 0)
			{ i = 1; j = 1; }
			else { i = -1; j = -1; }

			int startRow = pos.Row;
			int startCol = pos.Col;

			for (int r = 0; r < Range; r++)
			{
				j = j * -1;
				for (int c = 0; c < Range; c++)
				{
					i = i * -1;
					int row = startRow + (((r + 1) / 2) * j);
					int col = startCol + (((c + 1) / 2) * i);
					Grid.Add(new CellPosition(row, col));
				}
			}
			return Grid;
		}
		public string? GetStylePos(CellPosition pos, int angle)
		{
			int offsetX = 0;
			int offsetY = 0;

			var size = BoardConfig.CellSize;
			if (angle == 0)
			{
				offsetX = ((Range - 1) * -1 / 2);
				offsetY = ((Range - 1) * -1 / 2);
			}
			else if (angle == 90)
			{
				offsetX = 1;
				offsetY = ((Range - 1) * -1 / 2);
			}
			else if (angle == 180)
			{
				offsetX = ((Range + 1) * 1 / 2);
				offsetY = ((Range + 1) * 1 / 2);
			}
			else if (angle == 270)
			{
				offsetY = ((Range + 1) * 1 / 2);
			}
			else
			{ return null; }
			var top = pos.Row + offsetY;
			var left = pos.Col + offsetX + 1;
			var style = "";
			style += $"position: absolute;";
			style += $"top: {top * size}px;";
			style += $"left: {left * size}px;";
			style += $"width: {Range * size}px;";
			style += $"height: {Range * size}px;";
			style += $"background-image: url('{Path}');";
			style += "background-size: 100%;";
			style += $"transform: rotate({angle}deg);";
			style += "transform-origin: top left;";
			style += "pointer-events: none;";
			return style;
		}
	}
	public class Ship
	{

		public UnitType Id { get; set; }
		public TerrainType Terrain { get; init; }
		public int Price { get; init; }
		private int width, height;
		public int Width { get => IsVertical ? height : width; }
		public int Height { get => IsVertical ? width : height; }
		public required string ImagePath { get; set; }

		public int Angle { get; set; }
		public bool IsVertical => (Angle % 180) != 0;
		public bool IsShip => UnitTypeHelper.IsShip(Id);
		public bool IsSurvive(Dictionary<CellPosition, CellInfo> cellStates)
		{
			if (OccupyGrid == null) return false;

			return OccupyGrid.Any(pos =>
				cellStates.TryGetValue(pos, out var cell) && !cell.IsCheck
			);
		}

		private CellPosition centerPoint;
		public CellPosition CenterPoint
		{
			get => centerPoint;
			set
			{
				centerPoint = value;
				OccupyGrid = CalOccupyGrid(value);
			}
		}

		public HashSet<CellPosition>? OccupyGrid { get; private set; }
		private HashSet<CellPosition> CalOccupyGrid(CellPosition center)
		{
			var occupied = new HashSet<CellPosition>();
			int w = Width; int h = Height;
			int i = 1; int j = 1;
			if (Angle == 0) { i = 1; j = 1; }
			else if (Angle == 90) { i = -1; j = 1; }
			else if (Angle == 180) { i = -1; j = -1; }
			else if (Angle == 270) { i = 1; j = -1; }

			int startRow = center.Row;
			int startCol = center.Col;

			for (int r = 0; r < h; r++)
			{
				j = j * -1;
				for (int c = 0; c < w; c++)
				{
					i = i * -1;
					int row = startRow + (((r + 1) / 2) * j);
					int col = startCol + (((c + 1) / 2) * i);
					occupied.Add(new CellPosition(row, col));
				}
			}
			return occupied;
		}

		public static Ship FromTemplate(UnitTypeInfo template, int angle)
		{
			return new Ship
			{
				Id = template.Id,
				Angle = angle,
				width = template.Width,
				height = template.Height,
				ImagePath = template.UnitImagePath != null ? template.UnitImagePath : "",
				Terrain = template.Terrain,
				Price = template.Price,
			};
		}
		public string? GetStylePos()
		{
			var cellSize = BoardConfig.CellSize;

			int offsetX = 0;
			int offsetY = 0;
			int CntRow = CenterPoint.Row;
			int CntCol = CenterPoint.Col;

			if (Angle == 0)
			{
				offsetX = ((Width - 1) * -1 / 2);
				offsetY = ((Height - 1) * -1 / 2);
			}
			else if (Angle == 90)
			{
				offsetX = 1;
				offsetY = ((Height - 1) * -1 / 2);
			}
			else if (Angle == 180)
			{
				offsetX = ((Width + 1) * 1 / 2);
				offsetY = ((Height + 1) * 1 / 2);
			}
			else if (Angle == 270)
			{
				offsetY = ((Height + 1) * 1 / 2);
			}
			else
			{
				return null;
			}
			CntRow += offsetY;
			CntCol += offsetX + 1;

			var style = "";
			style += $"position: absolute;";
			style += $"top: {CntRow * cellSize}px;";
			style += $"left: {CntCol * cellSize}px;";
			style += $"width: {width * cellSize}px;";
			style += $"height: {height * cellSize}px;";
			style += $"background-image: url('{ImagePath}');";
			style += "background-size: 100%;";
			style += $"transform: rotate({Angle}deg);";
			style += "transform-origin: top left;";
			style += "pointer-events: none;";
			return style;
		}

		public void Rotate()
		{
			Angle = (Angle + 90) % 360;
		}

	}
	public class AIinfer
	{
		public int playerNum { get; init; }
		public OffsetArray<int> RowRemains { get; private set; }
		public OffsetArray<int> ColRemains { get; private set; }
		public AIinfer(int playerNum, BoardConfig board)
		{ 
			this.playerNum = playerNum;
			var startCol = playerNum == 0 ? board.Columns : 0;
			var endCol = playerNum==0? board.Columns*2-1 : board.Columns-1;
			ColRemains = new OffsetArray<int>(startCol, endCol);
			RowRemains = new OffsetArray<int>(0,board.Rows-1);
		}		
		public List<(UnitType, int, int)> TargetShip { get; set; } = new();
		public Dictionary<CellPosition, float> ProbabTable { get; private set; } = new();

		public class Rect
		{
			private CellPosition topLeft;
			private CellPosition downRight;
			public int width { get; init; }
			public int height { get; init; }
			public Rect(CellPosition startPos, CellPosition endPos)
			{
				if (startPos == endPos)
				{
					throw new ArgumentException("TopLeft and DownRight cannot be the same.");
				}
				int leftC = Math.Min(startPos.Col, endPos.Col);
				int topR = Math.Min(startPos.Row, endPos.Row);
				int rightC = Math.Max(startPos.Col, endPos.Col);
				int downR = Math.Max(startPos.Row, endPos.Row);
				this.topLeft = new CellPosition(topR, leftC);
				this.downRight = new CellPosition(downR, rightC);
				this.width = rightC - leftC + 1;
				this.height = downR - topR + 1;
				var feature = new HashSet<int>();
				if (rightC == leftC) { feature.Add(leftC + 1); }
				if (downR == topR) { feature.Add(-1 * (topR + 1)); }
				if (rightC != leftC && downR != topR)
				{
					if (rightC - leftC > downR - topR)
					{ for (int i = topR; i <= downR; i++) { feature.Add(-1 * (i + 1)); } }
					else
					{ for (int i = leftC; i <= rightC; i++) { feature.Add(i + 1); } }
				}
				this.DectFeature = feature;
			}
			public HashSet<int> DectFeature { get; init; }
			public CellPosition GetStartPos() { return topLeft; }
			public CellPosition GetEndPos() { return downRight; }
			public List<CellPosition> Reduction()
			{
				var startPos = this.topLeft;
				var endPos = this.downRight;
				int leftC = startPos.Col;
				int topR = startPos.Row;
				int rightC = endPos.Col;
				int downR = endPos.Row;
				List<CellPosition> locaGroup = new();
				if (this.width < this.height)
				{
					for (int c = leftC; c <= rightC; c++)
					{
						for (int r = topR; r <= downR; r++)
						{
							var pos = new CellPosition(r, c);
							locaGroup.Add(pos);
						}
					}
				}
				else
				{
					for (int r = topR; r <= downR; r++)
					{
						for (int c = leftC; c <= rightC; c++)
						{
							var pos = new CellPosition(r, c);
							locaGroup.Add(pos);
						}
					}
				}
				return locaGroup;
			}
		}
		private List<Rect> RectCluster { get; set; } = new();
		public Dictionary<CellPosition, float> GetProbabTable(ComplexData baseData)//暫時的方法
		{
			CalProbabTable(baseData);
			return ProbabTable;
		}

		private HashSet<CellPosition> EliminaPos = new();
		private Queue<(HashSet<CellPosition>, int)> NineGridProbab = new();
		private Queue<(HashSet<CellPosition>, int)> RanksProbab = new();
		private Dictionary<CellPosition, float> suspProbab = new(); private int suspTimes = 0;		private HashSet<CellPosition> ExposePos = new();		



		private void CalEliminaRowCol(ComplexData baseData)
		{
			var colStart = playerNum == 0 ? baseData.Board.Columns / 2 : 0;
			var colEnd = playerNum == 0 ? baseData.Board.Columns - 1 : baseData.Board.Columns / 2 - 1;
			var rowStart = 0; ; var rowEnd = baseData.Board.Rows - 1;
			var labelCol = playerNum == 0 ? baseData.Board.Columns : -1;
			var labelRow = baseData.Board.Rows;
			int possibleNum; int aware; int sinkNum;
			RanksProbab.Clear();
			for (int r = 0; r <= rowEnd; r++)
			{
				var labelPos = new CellPosition(r, labelCol);
				baseData.CellInfo.TryGetValue(labelPos, out var labelCell);
				if (labelCell != null && labelCell.IsDetect)
				{
					aware = 0; sinkNum = 0;
					HashSet<CellPosition> section = new HashSet<CellPosition>();
					for (int c = colStart; c <= colEnd; c++)
					{
						var pos = new CellPosition(r, c);
						baseData.CellInfo.TryGetValue(pos, out var revieCell);
						if (revieCell == null || revieCell.Terrain != TerrainType.Sea) { continue; }
						if (revieCell.IsCheck)
						{
							if (revieCell.OccupiedBy is Ship ship && ship.IsShip)
							{
								aware += 1;
								if (!ship.IsSurvive(baseData.CellInfo)) { sinkNum += 1; }
							}
							else
							{ EliminaPos.Add(pos); }
						}
						else
						{
							section.Add(pos);
						}
					}
					possibleNum = labelCell.DectNum - aware;
					RowRemains[r] = labelCell.DectNum - sinkNum;
					RanksProbab.Enqueue((section, possibleNum));
					if (possibleNum == 0)
					{
						RowRemains[r] = -1;
						for (int c = colStart; c <= colEnd; c++)
						{
							var pos = new CellPosition(r, c);
							EliminaPos.Add(pos);
						}
					}
				}
			}
			for (int c = colStart; c <= colEnd; c++)
			{
				var labelPos = new CellPosition(labelRow, c);
				baseData.CellInfo.TryGetValue(labelPos, out var labelCell);
				if (labelCell != null && labelCell.IsDetect)
				{
					aware = 0; sinkNum = 0;
					HashSet<CellPosition> section = new HashSet<CellPosition>();
					for (int r = rowStart; r <= rowEnd - 1; r++)
					{
						var pos = new CellPosition(r, c);
						baseData.CellInfo.TryGetValue(pos, out var revieCell);
						if (revieCell == null || revieCell.Terrain != TerrainType.Sea) { continue; }
						if (revieCell.IsCheck)
						{
							if (revieCell.OccupiedBy is Ship ship && ship.IsShip)
							{
								aware += 1;
								if (!ship.IsSurvive(baseData.CellInfo)) { sinkNum += 1; }
							}
							else
							{ EliminaPos.Add(pos); }
						}
						else
						{
							section.Add(pos);
						}
					}
					possibleNum = labelCell.DectNum - aware;
					ColRemains[c] = labelCell.DectNum - sinkNum;
					RanksProbab.Enqueue((section, possibleNum));
					if (possibleNum == 0)
					{
						ColRemains[c] = -1;
						for (int r = 0; r <= rowEnd; r++)
						{
							var pos = new CellPosition(r, c);
							EliminaPos.Add(pos);
						}
					}
				}
			}
		}
		private void CalRectPart(ComplexData baseData)
		{
			var colStart = playerNum == 0 ? baseData.Board.Columns / 2 : 0;
			var colEnd = playerNum == 0 ? baseData.Board.Columns - 1 : baseData.Board.Columns / 2 - 1;
			var rowStart = 0; var rowEnd = baseData.Board.Rows - 1;
			this.RectCluster.Clear();
			CalEliminaRowCol(baseData);
			CalJiugongge(baseData);
			List<Rect> lastCluster = new();
			//直向拆分
			for (int c = colStart; c <= colEnd; c++)
			{
				if (ColRemains[c] == -1) { lastCluster.Clear(); continue; }
				CellPosition startPos = new();
				bool isContinuous = false;
				List<Rect> currCluster = new();
				for (int r = rowStart; r <= rowEnd; r++)
				{
					var pos = new CellPosition(r, c);
					baseData.CellInfo.TryGetValue(pos, out var cell);
					if (cell == null) { continue; }
					bool isPossible = IsPossible(pos, baseData);
					if (isPossible)
					{
						if (!isContinuous) { startPos = pos; isContinuous = true; }
					}
					else
					{
						if (isContinuous && r - startPos.Row > 1)
						{
							var endPos = new CellPosition(r - 1, c);
							var rect = new Rect(startPos, endPos);
							currCluster.Add(rect);
						}
						isContinuous = false;
					}
					if (isContinuous && r == rowEnd && rowEnd - startPos.Row >= 1) { var rect = new Rect(startPos, pos); currCluster.Add(rect); }
				}
				this.RectCluster.AddRange(currCluster);
				if (currCluster.Count > 0 && lastCluster.Count > 0)
				{
					var mergerCluster = CalSecondMerge(lastCluster, currCluster);
					this.RectCluster.AddRange(mergerCluster);
				}
				lastCluster.Clear();
				lastCluster.AddRange(currCluster);
				currCluster.Clear();
			}
			lastCluster.Clear();
			//橫向拆分
			for (int r = rowStart; r <= rowEnd; r++)
			{
				if (RowRemains[r] == -1) { lastCluster.Clear(); continue; }
				CellPosition startPos = new();
				bool isContinuous = false;
				List<Rect> currCluster = new();
				for (int c = colStart; c <= colEnd; c++)
				{
					var pos = new CellPosition(r, c);
					baseData.CellInfo.TryGetValue(pos, out var cell);
					if (cell == null) { continue; }
					bool isPossible = IsPossible(pos, baseData);
					if (isPossible)
					{
						if (!isContinuous) { startPos = pos; isContinuous = true; }
					}
					else
					{
						if (isContinuous && c - startPos.Col > 1)
						{
							var endPos = new CellPosition(r, c - 1);
							var rect = new Rect(startPos, endPos);
							currCluster.Add(rect);
						}
						isContinuous = false;
					}
					if (isContinuous && c == colEnd && colEnd - startPos.Col >= 1) { var rect = new Rect(startPos, pos); currCluster.Add(rect); }
				}
				this.RectCluster.AddRange(currCluster);
				if (currCluster.Count > 0 && lastCluster.Count > 0)
				{
					var mergerCluster = CalSecondMerge(lastCluster, currCluster);
					this.RectCluster.AddRange(mergerCluster);
				}
				lastCluster.Clear();
				lastCluster.AddRange(currCluster);
				currCluster.Clear();
			}
		}
		private void CalProbabTable(ComplexData baseData)
		{
			if (TargetShip == null) { return; } //先假設
			var expose = baseData.CellInfo.Where(e => e.Value.IsCheck && e.Value.OccupiedBy is Ship ship
													  && ship.IsSurvive(baseData.CellInfo)).Select(k => k.Key).ToHashSet();
			ExposePos.Clear(); ExposePos.UnionWith(expose);
			CalRectPart(baseData);
			ProbabTable.Clear(); suspProbab.Clear(); suspTimes = 0;
			int shipWidth = TargetShip[0].Item2;
			int shipHeight = TargetShip[0].Item3;
			int times = 0;
			foreach (var rect in this.RectCluster)
			{
				var rectWidth = Math.Max(rect.width, rect.height);
				var rectHeight = Math.Min(rect.height, rect.width);
				bool isSuitable = shipWidth > rectWidth || shipHeight != rectHeight;
				bool dectPass = false;
				foreach (var f in rect.DectFeature)
				{
					if (f < 0)
					{
						var i = f * -1 - 1;
						if (RowRemains[i] >0 && shipWidth > RowRemains[i]){dectPass=true;break; }
					}
					else
					{
						var i = f - 1;
						if (ColRemains[i] > 0 && shipWidth > ColRemains[i]) { dectPass = true; break; }
					}
				}
				if (isSuitable|| dectPass) { continue; }

				List<CellPosition> posGroup = rect.Reduction();
				times += rectWidth - shipWidth + 1;
				int singleLength = posGroup.Count / rectHeight;
				CalSuspProb(posGroup, rectWidth, rectHeight);
				for (int i = 0; i < singleLength; i++)
				{
					for (int j = 0; j < rectHeight; j++)
					{
						var pos = posGroup[i + rectWidth * j];
						if (!ProbabTable.ContainsKey(pos)) { ProbabTable[pos] = 0.0f; }
						int peak = (rectWidth - Math.Abs(rectWidth - shipWidth * 2)) / 2 + 1;
						int k = i + 1;
						if (rectWidth % 2 != 0) //寬度是奇數
						{
							if (k * 2 < rectWidth)
							{ ProbabTable[pos] += Math.Min(k, peak); }
							else
							{ ProbabTable[pos] += Math.Min(rectWidth - k + 1, peak); }
						}
						else                    //寬度是偶數
						{
							if (k <= rectWidth / 2)
							{ ProbabTable[pos] += Math.Min(k, peak); }
							else
							{ ProbabTable[pos] += Math.Min(rectWidth - k + 1, peak); }
						}
					}

				}
			}
			foreach (var (Pos, amount) in ProbabTable)
			{
				var Prob = amount / times;
				float suspWeighted = 0.0f;
				if (suspProbab.ContainsKey(Pos))
				{
					var suspAmount = suspProbab[Pos];
					suspWeighted = suspAmount / suspTimes;
				}
				Prob = 1.0f - ((1.0f - Prob) * (1.0f - suspWeighted));
				baseData.CellInfo.TryGetValue(Pos, out var cell);
				if (cell == null) { continue; }
				ProbabTable[Pos] =  cell.IsCheck ? 0.0f:Prob;
			}
			while (NineGridProbab.Count > 0)
			{
				var grid = NineGridProbab.Dequeue();
				if (grid.Item1.Count == 0) { continue; };
				var nineProb = (float)grid.Item2 / grid.Item1.Count;
				foreach (var pos in grid.Item1)
				{
					baseData.CellInfo.TryGetValue(pos, out var cell);
					if (cell == null || cell.IsCheck) { continue; }
					if (ProbabTable.ContainsKey(pos))
					{
						var Prob = 1 - (1 - ProbabTable[pos]) * (1 - nineProb);
						ProbabTable[pos] = Prob;
					}
					else { continue; }
				}
			}
			while (RanksProbab.Count > 0)
			{
				var ranks = RanksProbab.Dequeue();
				if (ranks.Item1.Count == 0) { continue; };
				var rankProb = (float)ranks.Item2 / ranks.Item1.Count;
				foreach (var pos in ranks.Item1)
				{
					baseData.CellInfo.TryGetValue(pos, out var cell);
					if (cell == null || cell.IsCheck) { continue; }
					if (ProbabTable.ContainsKey(pos))
					{
						var Prob = 1 - (1 - ProbabTable[pos]) * (1 - rankProb);
						ProbabTable[pos] = Prob;
					}
					else { continue; }
				}
			}
		}
		private static List<Rect> CalSecondMerge(List<Rect> lastcluster, List<Rect> cuurcluster)
		{
			List<(int, bool)> integrator = new();
			List<Rect> merge = new();
			//列整併計算			
			if (lastcluster.All(last => last.width == 1) && cuurcluster.All(cuur => cuur.width == 1))
			{
				var lastEndA = lastcluster.FindLast(n => n.height > 2);
				var firstStartA = cuurcluster.Find(n => n.height > 2);
				var lastEndB = cuurcluster.FindLast(n => n.height > 2);
				var firstStartB = lastcluster.Find(n => n.height > 2);

				if (lastEndA == null || firstStartA == null || lastEndB == null || firstStartB == null)
					return merge;

				if (!(lastEndA.GetEndPos().Row > firstStartA.GetStartPos().Row
				   && lastEndB.GetEndPos().Row > firstStartB.GetStartPos().Row))
					return merge;

				int leftC = lastcluster[0].GetStartPos().Col;
				int rightC = cuurcluster[0].GetEndPos().Col;
				foreach (var leftRrect in lastcluster)
				{
					int s = leftRrect.GetStartPos().Row;
					int e = leftRrect.GetEndPos().Row;
					integrator.Add((s, true)); integrator.Add((e, false));
				}
				foreach (var rightRrect in cuurcluster)
				{
					int s = rightRrect.GetStartPos().Row;
					int e = rightRrect.GetEndPos().Row;
					integrator.Add((s, true)); integrator.Add((e, false));
				}
				integrator.Sort((a, b) => a.Item1.CompareTo(b.Item1));
				for (int i = 0; i < integrator.Count - 1; i++)
				{
					bool legalLength = Math.Abs(integrator[i].Item1 - integrator[i + 1].Item1) >= 2;
					bool isCorrect = (integrator[i].Item2 == true && integrator[i + 1].Item2 == false);
					if (legalLength && isCorrect)
					{
						var endPos = new CellPosition(integrator[i + 1].Item1, rightC);
						var startPos = new CellPosition(integrator[i].Item1, leftC);
						var rect = new Rect(startPos, endPos);
						merge.Add(rect);
					}
				}
			}
			integrator.Clear();
			//行整併計算
			if (lastcluster.All(last => last.height == 1) && cuurcluster.All(cuur => cuur.height == 1))
			{
				var lastEndA = lastcluster.FindLast(n => n.width > 2);
				var firstStartA = cuurcluster.Find(n => n.width > 2);
				var lastEndB = cuurcluster.FindLast(n => n.width > 2);
				var firstStartB = lastcluster.Find(n => n.width > 2);

				if (lastEndA == null || firstStartA == null || lastEndB == null || firstStartB == null)
					return merge;

				if (!(lastEndA.GetEndPos().Col > firstStartA.GetStartPos().Col
				   && lastEndB.GetEndPos().Col > firstStartB.GetStartPos().Col))
					return merge;

				int topR = lastcluster[0].GetStartPos().Row;
				int downR = cuurcluster[0].GetEndPos().Row;
				foreach (var leftRrect in lastcluster)
				{
					int s = leftRrect.GetStartPos().Col;
					int e = leftRrect.GetEndPos().Col;
					integrator.Add((s, true)); integrator.Add((e, false));
				}
				foreach (var rightRrect in cuurcluster)
				{
					int s = rightRrect.GetStartPos().Col;
					int e = rightRrect.GetEndPos().Col;
					integrator.Add((s, true)); integrator.Add((e, false));
				}
				integrator.Sort((a, b) => a.Item1.CompareTo(b.Item1));
				for (int i = 0; i < integrator.Count - 1; i++)
				{
					bool legalLength = Math.Abs(integrator[i].Item1 - integrator[i + 1].Item1) >= 2;
					bool isCorrect = (integrator[i].Item2 == true && integrator[i + 1].Item2 == false);
					if (legalLength && isCorrect)
					{
						var endPos = new CellPosition(downR, integrator[i + 1].Item1);
						var startPos = new CellPosition(topR, integrator[i].Item1);
						var rect = new Rect(startPos, endPos);
						merge.Add(rect);
					}
				}
			}
			return merge;
		}
		private bool IsPossible(CellPosition pos, ComplexData baseData)
		{
			baseData.CellInfo.TryGetValue(pos, out var cell);
			if (cell != null)
			{
				if (cell.Terrain != TerrainType.Sea) { return false; }
				if (cell.IsCheck)
				{
					if (ExposePos.Contains(pos)) { return true; }
					else { return false; }
				}
				if (EliminaPos.Contains(pos)) { return false; }
			}
			else
			{
				return false;
			}
			return true;
		}
		public void CalJiugongge(ComplexData baseData)
		{
			var startCol = playerNum == 0 ? baseData.Board.Columns / 2 : 0;
			var endCol = playerNum == 0 ? baseData.Board.Columns - 1 : baseData.Board.Columns / 2 - 1;
			var startRow = 0;
			var endRow = baseData.Board.Rows - 1;
			NineGridProbab.Clear();
			List<(CellPosition, int)> DectPosCollect =
												baseData.CellInfo
												.Where(k => k.Value.IsDetect &&
												(k.Key.Col < endCol && k.Key.Col > startCol) && (k.Key.Row < endRow && k.Key.Row > startRow)
												).Select(k => (k.Key, k.Value.DectNum)).ToList();
			if (DectPosCollect.Count == 0) { return; }
			List<(HashSet<CellPosition>, int)> Jiugongs = new();
			foreach (var cell in DectPosCollect)
			{
				int r = cell.Item1.Row;
				int c = cell.Item1.Col;
				HashSet<CellPosition> quare = new HashSet<CellPosition>();
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						var pos = new CellPosition(r + i, c + j);
						quare.Add(pos);
						baseData.CellInfo.TryGetValue(pos, out var info);
						if (info == null) { continue; }
						if (info.IsCheck) { EliminaPos.Add(pos); }
					}
				}
				Jiugongs.Add((quare, cell.Item2));
			}
			if (Jiugongs.Count == 0) { return; }
			List<int> impleNum = Enumerable.Range(0, Jiugongs.Count).ToList();
			int k = 0; int firstTrip = Jiugongs.Count;
			do
			{
				var (range, num) = Jiugongs[impleNum[k]];
				if (k < firstTrip)
				{
					foreach (var pos in range)
					{
						baseData.CellInfo.TryGetValue(pos, out var info);
						if (info == null) { continue; }
						if (info.OccupiedBy is Ship ship && info.IsCheck && ship.IsShip) { num -= 1; }
					}
				}
				var scope = range.Except(EliminaPos).ToHashSet();
				Jiugongs[impleNum[k]] = (scope, num);
				if (num <= 0)
				{
					for (int q = 0; q < k; q++)
					{
						int n = scope.Intersect(Jiugongs[impleNum[q]].Item1).Count();
						if (n > 0) { impleNum.Add(impleNum[q]); }
					}
					EliminaPos.UnionWith(scope);
				}
				k += 1;
			}
			while (k < impleNum.Count);
			foreach (var part in Jiugongs)
			{
				if (part.Item2 > 0)
				{
					NineGridProbab.Enqueue(part);
				}
			}
		}
		private void CalSuspProb(List<CellPosition> posGroup, int rectWidth, int rectHeight)
		{
			var suspPos = posGroup.Intersect(ExposePos).ToList();
			if (suspPos.Count == 0) { return; }
			var shipWidth = TargetShip[0].Item2;
			int times = 0;
			foreach (var susp in suspPos)
			{
				var num = posGroup.IndexOf(susp) % rectWidth;
				var head = Math.Max(num - shipWidth + 1, 0);
				var tail = Math.Min(num + shipWidth, rectWidth) - 1;
				var wide = tail - head + 1;
				var peak = wide - shipWidth + 1;
				times += peak;
				for (int i = head; i <= tail; i++)
				{
					for (int j = 0; j < rectHeight; j++)
					{
						var serial = i + rectWidth * j;
						var pos = posGroup[serial];
						if (!suspProbab.ContainsKey(pos)) { suspProbab[pos] = 0.0f; }
						var amount = suspProbab[pos];
						int k = i - head + 1;
						if (wide % 2 != 0) //寬度是奇數
						{
							if (k * 2 < wide)
							{ amount += Math.Min(k, peak); }
							else
							{ amount += Math.Min(wide - k + 1, peak); }
						}
						else                    //寬度是偶數
						{
							if (k <= wide / 2)
							{ amount += Math.Min(k, peak); }
							else
							{ amount += Math.Min(wide - k + 1, peak); }
						}
						suspProbab[pos] = amount;
					}
				}
			}
			suspTimes += times;
		}
	}	
	public class OffsetArray<T>
	{
		private readonly T[] data;
		private readonly int offset;
		public OffsetArray(int minIndex, int maxIndex)
		{
			offset = minIndex;
			data = new T[maxIndex - minIndex + 1];
		}

		public T this[int index]
		{
			get => data[index - offset];
			set => data[index - offset] = value;
		}

		public int Length => data.Length;
	}
}

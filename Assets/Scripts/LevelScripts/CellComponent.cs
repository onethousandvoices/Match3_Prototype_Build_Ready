﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Match3
{
    public class CellComponent : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public ChipComponent[] _chipPrefabs;
        public ChipComponent Chip;
        private ChipComponent _previousChip;
        private LinkedList<CellComponent> _poolingList;
        private int _cellsLayer;
        private int _spawnsLayer;
        private SpawnPointComponent _spawnPoint;
        private CellComponent _top;
        private CellComponent _topTop;
        private CellComponent _bot;
        private CellComponent _botBot;
        private CellComponent _left;
        private CellComponent _leftLeft;
        private CellComponent _right;
        private CellComponent _rightRight;
        public event Action<CellComponent,Vector2> PointerDownEvent;
        public event Action<CellComponent> PointerUpEvent;
        public bool IsMatch { get; private set; }

        private void Start()
        {
            _poolingList = new LinkedList<CellComponent>();
            _cellsLayer = LayerMask.GetMask("Level");
            _spawnsLayer = LayerMask.GetMask("Spawn");
            FindNeighbours();
            StartCoroutine(GenerateChipRoutine());
        }

        private bool CompareChips(CellComponent comparativeCell)
        {
            return comparativeCell.NotNull() &&
                   comparativeCell.Chip.NotNull() &&
                   comparativeCell.Chip != Chip &&
                   comparativeCell.Chip.Type == Chip.Type;
        }

        private void Pulling(params CellComponent[] cells)
        {
            if (!_poolingList.Contains(this)) _poolingList.AddLast(this);

            foreach (CellComponent cellComponent in cells)
            {
                if (_poolingList.Contains(cellComponent)) continue;
                _poolingList.AddLast(cellComponent);
            }
        }

        public void CheckMatches(ChipComponent newChip)
        {
            _previousChip = Chip;
            Chip = newChip;
            Chip.transform.parent = transform;
            IsMatch = false;
            _poolingList.Clear();

            #region Horizontal
            if (CompareChips(_left) && CompareChips(_right))
            {
                //00_00
                if (CompareChips(_leftLeft) && CompareChips(_rightRight)) Pulling(_leftLeft, _rightRight);
                //00_0
                if (CompareChips(_leftLeft)) Pulling(_leftLeft);
                //0_00
                if (CompareChips(_rightRight)) Pulling(_rightRight);
                //0_0
                Pulling(_left, _right);
            }
            //00_
            if (CompareChips(_left) && CompareChips(_leftLeft)) Pulling(_left, _leftLeft);
            //_00
            if (CompareChips(_right) && CompareChips(_rightRight)) Pulling(_right, _rightRight);
            #endregion

            #region Vertical
            if (CompareChips(_top) && CompareChips(_bot)) //top is left
            {
                //00_00
                if (CompareChips(_topTop) && CompareChips(_botBot)) Pulling(_topTop, _botBot);
                //00_0
                if (CompareChips(_topTop)) Pulling(_topTop);
                //0_00
                if (CompareChips(_botBot)) Pulling(_botBot);
                //0_0
                Pulling(_top, _bot);
            }
            //00_
            if (CompareChips(_top) && CompareChips(_topTop)) Pulling(_top, _topTop);
            //_00
            if (CompareChips(_bot) && CompareChips(_botBot)) Pulling(_bot, _botBot);
            #endregion

            if (_poolingList.Count != 0)
            {
                IsMatch = true;
                StartCoroutine(MatchRoutine());
            }
            else
            {
                IsMatch = false;
                StartCoroutine(NoMatchRoutine());
            }
        }

        private IEnumerator MatchRoutine()
        {
            Chip.CurrentCell = this;
            yield return new WaitWhile(() => _poolingList.Any(z => z.Chip.IsAnimating));
            foreach (CellComponent cell in _poolingList.OrderBy(z => z.transform.position.y))
                StartCoroutine(cell.ChipFadingRoutine());

        }

        public IEnumerator ChipFadingRoutine()
        {
            Chip.FadeOut();
            _previousChip = Chip;
            Chip = null;
            yield return new WaitWhile(() => _previousChip.IsAnimating);
            Kidnapping(_top);
        }

        private IEnumerator NoMatchRoutine()
        {
            yield return new WaitWhile(() => Chip.IsAnimating);
            if (_previousChip.GetMatchState() || IsMatch) yield break;
            Chip.MoveBack();
            Chip = _previousChip;
        }

        public void Kidnapping(CellComponent topNeighbour) // :)
        {
            if (topNeighbour.NotNull() && topNeighbour.Chip.NotNull())
            {
                if (topNeighbour.Chip.ReservedBy.IsNull())
                {
                    print($"{name}{topNeighbour.name}");
                    topNeighbour.Chip.ReservedBy = this;
                    //topNeighbour.Chip.Transfer(this, true);
                }
                else Kidnapping(topNeighbour.GetNeighbour(DirectionType.Top));

            }
            else if (topNeighbour.NotNull() && topNeighbour.Chip.IsNull())
            {
                Kidnapping(topNeighbour.GetNeighbour(DirectionType.Top));
            }
            else _spawnPoint.GenerateChip(this);
        }

        private void FindNeighbours()
        {
            RaycastHit2D topRay = Physics2D.Raycast(transform.position, transform.up, 1f, _cellsLayer);
            RaycastHit2D botRay = Physics2D.Raycast(transform.position, -transform.up, 1f, _cellsLayer);
            RaycastHit2D leftRay = Physics2D.Raycast(transform.position, -transform.right, 1f, _cellsLayer);
            RaycastHit2D rightRay = Physics2D.Raycast(transform.position, transform.right, 1f, _cellsLayer);
            RaycastHit2D spawnRay = Physics2D.Raycast(transform.position, transform.up, 10f, _spawnsLayer);

            if (topRay.collider.NotNull()) _top = topRay.collider.GetComponent<CellComponent>();
            if (botRay.collider.NotNull()) _bot = botRay.collider.GetComponent<CellComponent>();
            if (leftRay.collider.NotNull()) _left = leftRay.collider.GetComponent<CellComponent>();
            if (rightRay.collider.NotNull()) _right = rightRay.collider.GetComponent<CellComponent>();

            if (spawnRay.collider.NotNull()) _spawnPoint = spawnRay.collider.GetComponent<SpawnPointComponent>();

            StartCoroutine(FindExtraNeighbours());
        }

        private IEnumerator FindExtraNeighbours()
        {
            yield return null;
            _topTop = _top.NotNull()
                ? _top.GetNeighbour(DirectionType.Top)
                : null;
            _botBot = _bot.NotNull()
                ? _bot.GetNeighbour(DirectionType.Bot)
                : null;
            _leftLeft = _left.NotNull()
                ? _left.GetNeighbour(DirectionType.Left)
                : null;
            _rightRight = _right.NotNull()
                ? _right.GetNeighbour(DirectionType.Right)
                : null;
        }

        private IEnumerator GenerateChipRoutine()
        {
            ChipInstance(_chipPrefabs);
            yield return null;

            if ((!CompareChips(_top) || !CompareChips(_bot)) && (!CompareChips(_left) || !CompareChips(_right))) yield break;
            Pool.Singleton.Pull(Chip);

            CellComponent[] neighbours = { _top, _bot, _left, _right };
            var allowedChips = _chipPrefabs.Where(z => !neighbours.Where(x => x.NotNull())
                                                                  .Select(c => c.Chip.Type)
                                                                  .Contains(z.Type)).ToArray();

            ChipInstance(allowedChips);
        }

        private void ChipInstance(IReadOnlyList<ChipComponent> array)
        {
            Chip = Instantiate(array[UnityEngine.Random.Range(0, array.Count)], transform);
            Chip.CurrentCell = this;
        }

        public CellComponent GetNeighbour(DirectionType direction)
        {
            switch (direction)
            {
                case DirectionType.Top:
                    return _top;
                case DirectionType.Bot:
                    return _bot;
                case DirectionType.Left:
                    return _left;
                case DirectionType.Right:
                    return _right;
                default: return null;
            }
        }

        public void OnPointerDown(PointerEventData eventData) => PointerDownEvent?.Invoke(this, eventData.position);
        public void OnPointerUp(PointerEventData eventData) => PointerUpEvent?.Invoke(this);
    }
}
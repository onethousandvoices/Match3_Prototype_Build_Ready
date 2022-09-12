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
        public bool IsMatch { get; private set; }
        public ChipComponent CurrentChip { get; private set; }
        private ChipComponent _previousChip;
        [SerializeField] //todo убрать
        private ChipComponent _specialChip;
        private int _cellsLayer;
        private int _spawnsLayer;
        private CellComponent _top;
        private CellComponent _topTop;
        private CellComponent _bot;
        private CellComponent _botBot;
        private CellComponent _left;
        private CellComponent _leftLeft;
        private CellComponent _right;
        private CellComponent _rightRight;
        private CellComponent PulledBy { get; set; }
        private List<CellComponent> _poolingNeighbours;
        public SpawnPointComponent SpawnPoint { get; private set; }
        public event Action<CellComponent,Vector2> PointerDownEvent;
        public event Action<CellComponent> PointerUpEvent;

        private void Start()
        {
            _poolingNeighbours = new List<CellComponent>();
            _cellsLayer = LayerMask.GetMask("Level");
            _spawnsLayer = LayerMask.GetMask("Spawn");
            FindNeighbours();
            StartCoroutine(GenerateChipRoutine());
        }

        public void CheckMatches(ChipComponent newChip)
        {
            SetPreviousChip(CurrentChip);
            SetCurrentChip(newChip);
            if (newChip.Type == ChipType.None)
            {
                StartCoroutine(MatchRoutine());
                IsMatch = true;
                return;
            }
            IsMatch = false;

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
            StartCoroutine(_poolingNeighbours.Count > 0
                ? MatchRoutine()
                : NoMatchRoutine());
        }

        private IEnumerator MatchRoutine()
        {
            IsMatch = true;
            CurrentChip.CurrentCell = this;
            if (CurrentChip.Type == ChipType.None)
            {
                yield return new WaitWhile(() => CurrentChip.IsAnimating);
                CurrentChip.GetComponent<SpecialChipComponent>().Action();
            }

            yield return new WaitWhile(() => _poolingNeighbours
                                             .Where(z => z.CurrentChip.NotNull())
                                             .Any(x => x.CurrentChip.IsAnimating));
            Pooling();
        }

        private IEnumerator NoMatchRoutine()
        {
            IsMatch = false;
            if (CurrentChip.IsTransferred)
            {
                CurrentChip.EndTransfer();
                yield break;
            }

            yield return new WaitWhile(() => CurrentChip.IsAnimating);
            CurrentChip.SetInteractionState(true);

            if (_previousChip.GetMatchState() || IsMatch)
                yield break;

            StartCoroutine(CurrentChip.MoveBackRoutine());
            SetCurrentChip(_previousChip);
        }

        public void Pulling(params CellComponent[] cells)
        {
            if (!_poolingNeighbours.Contains(this))
                _poolingNeighbours.Add(this);

            foreach (CellComponent cell in cells)
            {
                if (_poolingNeighbours.Contains(cell)) continue;
                _poolingNeighbours.Add(cell);

                cell.CurrentChip.SetInteractionState(false);

                if (cell.PulledBy.IsNull())
                    cell.SetPulledByCell(this);

                else if (cell.PulledBy.NotNull())
                {
                    cell.PulledBy._poolingNeighbours.AddRange(_poolingNeighbours);

                    foreach (CellComponent pulledCell in cell.PulledBy._poolingNeighbours)
                        pulledCell.SetPulledByCell(this);
                }
            }
        }

        private void Pooling()
        {
            if (PulledBy.NotNull() && PulledBy != this)
            {
                PulledBy._poolingNeighbours.AddRange(_poolingNeighbours);
                _poolingNeighbours.Clear();
                return;
            }

            LevelManager.Singleton.DestroyChips(_poolingNeighbours.Distinct().ToList(), this);

            _poolingNeighbours.Clear();
        }

        public IEnumerator TransferChip(CellComponent callerCell)
        {
            CurrentChip.Transfer(callerCell);
            yield return null;
            LevelManager.Singleton.GetNewChip(this);
        }

        public IEnumerator ChipFadingRoutine()
        {
            if (CurrentChip.IsNull()) yield break;
            CurrentChip.FadeOut();
            SetPreviousChip(CurrentChip);
            SetCurrentChip(null);
            if (_specialChip.NotNull())
            {
                SetCurrentChip(_specialChip);
                _specialChip.ShowUp();
            }

            yield return new WaitWhile(() => _previousChip.IsAnimating);
            SetPreviousChip(null);
            if (_specialChip.NotNull())
            {
                _specialChip = null;
                yield break;
            }

            LevelManager.Singleton.GetNewChip(this);
        }

        private bool CompareChips(CellComponent comparativeCell)
        {
            return comparativeCell.NotNull() &&
                   comparativeCell.CurrentChip.NotNull() &&
                   comparativeCell.CurrentChip != CurrentChip &&
                   comparativeCell.CurrentChip.Type == CurrentChip.Type;
        }

        private void SetPreviousChip(ChipComponent newChip) => _previousChip = newChip;
        public void SetCurrentChip(ChipComponent newChip) => CurrentChip = newChip;
        public void SetSpecialChip(ChipComponent newChip) => _specialChip = newChip;
        public void SetPulledByCell(CellComponent cell) => PulledBy = cell;

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
            if (spawnRay.collider.NotNull()) SpawnPoint = spawnRay.collider.GetComponent<SpawnPointComponent>();

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
            if (_specialChip.NotNull())//todo
            {
                SetCurrentChip(Instantiate(_specialChip, transform));
                CurrentChip.CurrentCell = this;
                yield break;
            }
            ChipInstance(LevelManager.Singleton.ChipPrefabs);
            yield return null;

            if ((!CompareChips(_top) || !CompareChips(_bot)) && (!CompareChips(_left) || !CompareChips(_right))) yield break;
            Pool.Singleton.Pull(CurrentChip);

            CellComponent[] neighbours = { _top, _bot, _left, _right };
            var allowedChips = LevelManager.Singleton.ChipPrefabs.Where(z => !neighbours.Where(x => x.NotNull())
                                                                  .Select(c => c.CurrentChip.Type)
                                                                  .Contains(z.Type)).ToArray();

            ChipInstance(allowedChips);
        }

        private void ChipInstance(IReadOnlyList<ChipComponent> array)
        {
            SetCurrentChip(Instantiate(array[UnityEngine.Random.Range(0, array.Count-LevelManager.Singleton.REMOVE)], transform));
            CurrentChip.CurrentCell = this;
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
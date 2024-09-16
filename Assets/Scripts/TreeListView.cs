using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum Direction
{
    Horizontal,
    Vertical
}

[RequireComponent(typeof(ScrollRect))]
public class TreeListView : MonoBehaviour
{
    [Header("组件")]
    public TreeParentItemCell parentItem;
    public TreeChildItemCell childItem;

    [Header("参数")]
    public float parentSpaceValue = 0f;
    public float childSpaceValue = 0;
    public bool onlySelectSingle = false;
    // public bool isDynamicSize = false;
    public Direction listDirection = Direction.Vertical;

    #region 初始化获取的数据
    
    private ScrollRect _thisScrollRect;
    private RectTransform _contentRect;
    private RectTransform _parentItemRect;
    private RectTransform _childItemRect;
    private Vector2 _parentItemSize;
    private Vector2 _childItemSize;
    private Vector2 _viewportSize;

    #endregion

    #region 列表缓存数据

    private bool _isInit;
    private List<TreeParentData> _treeDataList;
    private int _selectParentIdx, _selectChildIdx;
    private Dictionary<int, bool> _expandDict = new Dictionary<int, bool>();
    private List<TreeParentItemCell> _usingParentItemList = new List<TreeParentItemCell>();
    private List<TreeChildItemCell> _usingChildItemList = new List<TreeChildItemCell>();

    #endregion

    public Action<TreeParentData, int, TreeParentItemCell> OnSelectParent;
    public Action<object, int, TreeChildItemCell> OnSelectChild;
    
    #region 外部调用

    private void InitTreeList()
    {
        parentItem ??= this.transform.GetComponentInChildren<TreeParentItemCell>();
        childItem ??= this.transform.GetComponentInChildren<TreeChildItemCell>();
        
        _thisScrollRect = this.transform.GetComponent<ScrollRect>();
        _contentRect = _thisScrollRect.content.GetComponent<RectTransform>();
        var viewportRect = _thisScrollRect.viewport.GetComponent<RectTransform>().rect;
        _viewportSize = new Vector2(viewportRect.width, viewportRect.height);
        //用 Elastic 时 onValueChanged 会多很多消耗
        if (_thisScrollRect.movementType == ScrollRect.MovementType.Elastic)
            _thisScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _thisScrollRect.vertical = listDirection == Direction.Vertical;
        _thisScrollRect.horizontal = listDirection == Direction.Horizontal;
        _thisScrollRect.onValueChanged.AddListener(OnScrollValueChanged);

        _parentItemRect = parentItem.GetComponent<RectTransform>();
        _childItemRect = childItem.GetComponent<RectTransform>();
        _parentItemSize = _parentItemRect.sizeDelta;
        _childItemSize = _childItemRect.sizeDelta;
        _isInit = true;
    }
    
    public void ShowTreeList(List<TreeParentData> dataList, int selectParentIdx = 0, int selectChild = 0)
    {
        if (!_isInit) InitTreeList();
        
        _expandDict[selectParentIdx] = true;
        _selectParentIdx = selectParentIdx;
        _selectChildIdx = selectChild;
        _contentRect.anchoredPosition = new Vector2(0, 0);
        RecycleAllItem();
        ForceRefresh(dataList);
    }

    public void ForceRefresh(List<TreeParentData> dataList = null)
    {
        if (dataList != null)
            _treeDataList = dataList;
        RefreshContentRectSize();
        RefreshVisibleItem();
    }

    public void RefreshParentItem(int idx = -1)
    {
        if (idx == -1)
        {
            foreach (var item in _usingParentItemList)
                item.OnRefreshItem();
            return;
        }
        var selectItem = _usingParentItemList.Find(item => item.parentCellId == idx);
        selectItem?.OnRefreshItem();
    }

    public void RefreshChildItem(int parentIdx = -1, int childIdx = -1)
    {
        if (parentIdx == -1 || childIdx == -1)
        {
            foreach (var item in _usingChildItemList)
                item.OnRefreshItem();
            return;
        }
        var selectItem = _usingChildItemList.Find(item => item.parentCellId == parentIdx && item.childCellId == childIdx);
        selectItem?.OnRefreshItem();
    }

    #endregion

    #region 内部方法
    
    private void RefreshContentRectSize()
    {
        var newSize = new Vector2(_parentItemSize.x, _parentItemSize.y);
        var showChildNum = _treeDataList.Where((t, i) => CheckIsExpand(i)).Sum(t => t.ChildDataList.Count);
        if (listDirection == Direction.Vertical)
        {
            var allParentH = _treeDataList.Count * _parentItemSize.y + _treeDataList.Count * parentSpaceValue;
            var allChildH = showChildNum * _childItemSize.y + showChildNum  * childSpaceValue;
            newSize.y = allParentH + allChildH - parentSpaceValue;
        }
        else
        {
            var allParentW = _treeDataList.Count * _parentItemSize.x + _treeDataList.Count * parentSpaceValue;
            var allChildW = showChildNum * _childItemSize.x + showChildNum * childSpaceValue;
            newSize.x = allParentW + allChildW - parentSpaceValue;
        }
        _contentRect.sizeDelta = newSize;
    }

    private bool CheckIsExpand(int parentIdx)
    {
        if (onlySelectSingle)
            return _selectParentIdx == parentIdx;
        return _expandDict.TryGetValue(parentIdx, out var value) && value;
    }

    private void OnScrollValueChanged(Vector2 arg0)
    {
        // TODO Elastic 优化待定
        // switch (listDirection)
        // {
        //     case Direction.Vertical when arg0.y is < 0 or > 1 :
        //     case Direction.Horizontal when  arg0.x is < 0 or > 1 :
        //         return;
        // }
        RecheckItemShow();
    }

    private void RecheckItemShow()
    {
        if (_treeDataList.Count <= 0) return;

        var passPosDict = new Dictionary<Vector2, bool>();
        for (var index = _usingParentItemList.Count; index > 0; index--)
        {
            var itemCell = _usingParentItemList[index - 1];
            if (!CanBeSeeInList(itemCell.rectTransform.anchoredPosition, itemCell.rectTransform.sizeDelta))
            {
                SetParentItemBackPool(itemCell);
                _usingParentItemList.RemoveAt(index - 1);
            }
            else
                passPosDict[itemCell.rectTransform.anchoredPosition] = true;
        }

        if (_usingChildItemList.Count > 0)
        {
            for (var index = _usingChildItemList.Count; index > 0; index--)
            {
                var itemCell = _usingChildItemList[index - 1];
                if (!CanBeSeeInList(itemCell.rectTransform.anchoredPosition, itemCell.rectTransform.sizeDelta))
                {
                    SetChildItemBackPool(itemCell);
                    _usingChildItemList.RemoveAt(index - 1);
                }
                else
                    passPosDict[itemCell.rectTransform.anchoredPosition] = true;
            }
        }

        RefreshVisibleItem(false, passPosDict);
    }

    private void RefreshVisibleItem(bool recycleAll = true, Dictionary<Vector2, bool> passPosDict = null)
    {
        if (recycleAll) RecycleAllItem();
        var pos = new Vector2(0, 0);
        for (int parentIdx = 0; parentIdx < _treeDataList.Count; parentIdx++)
        {
            var parentData = _treeDataList[parentIdx];
            var isExpand = CheckIsExpand(parentIdx);
            if (CanBeSeeInList(pos, _parentItemSize))
            {
                if (passPosDict == null || !passPosDict.ContainsKey(pos))
                {
                    var parentItemCell = GetParentItemInPool(pos);
                    parentItemCell.parentCellId = parentIdx;
                    parentItemCell.SetData(parentData);
                    parentItemCell.RefreshExpandShow(isExpand);
                }
            }
            
            var parentSpace = (isExpand ? childSpaceValue : parentSpaceValue);
            if (listDirection == Direction.Vertical)
                pos.y -= _parentItemSize.y + parentSpace;
            else
                pos.x += _parentItemSize.x + parentSpace;

            if (!isExpand) continue;

            for (int childIdx = 0; childIdx < parentData.ChildDataList.Count; childIdx++)
            {
                if (CanBeSeeInList(pos, _childItemSize))
                {
                    if (passPosDict == null || !passPosDict.ContainsKey(pos))
                    {
                        var childItemCell = GetChildItemInPool(pos);
                        childItemCell.parentCellId = parentIdx;
                        childItemCell.childCellId = childIdx;
                        childItemCell.SetData(parentData.ChildDataList[childIdx]);
                        childItemCell.RefreshSelectShow(_selectParentIdx == parentIdx && _selectChildIdx == childIdx);
                    }
                };

                var childSpace = (childIdx + 1 >= parentData.ChildDataList.Count ? parentSpaceValue : childSpaceValue);
                if (listDirection == Direction.Vertical)
                    pos.y -= _childItemSize.y + childSpace;
                else
                    pos.x += _childItemSize.x + childSpace;
            }
        }
    }

    private bool CanBeSeeInList(Vector2 pos, Vector2 size)
    {
        if (listDirection == Direction.Vertical)
        {
            return -pos.y + size.y > _contentRect.anchoredPosition.y && -pos.y < _contentRect.anchoredPosition.y + _viewportSize.y;
        }
        return pos.x + size.x > -_contentRect.anchoredPosition.x && pos.x < -_contentRect.anchoredPosition.x + _viewportSize.x;
    }

    private void RecycleAllItem()
    {
        if (_usingParentItemList.Count > 0)
        {
            foreach (var item in _usingParentItemList)
                SetParentItemBackPool(item);
            _usingParentItemList.Clear();
        }
        
        if (_usingChildItemList.Count > 0)
        {
            foreach (var item in _usingChildItemList)
                SetChildItemBackPool(item);
            _usingChildItemList.Clear();
        }
    }

    private void OnClickParentEvent(TreeParentItemCell itemCell)
    {
        var idx = itemCell.parentCellId;
        _selectParentIdx = idx;
        _expandDict[idx] = !CheckIsExpand(idx);
        ForceRefresh();
        if (!CheckIsExpand(idx)) return;
        OnSelectParent?.Invoke(itemCell.Data, idx, itemCell);
    }

    private void OnClickChildEvent(TreeChildItemCell itemCell)
    {
        _selectChildIdx = itemCell.childCellId;
        OnSelectChild?.Invoke(itemCell.Data, _selectChildIdx, itemCell);
        itemCell.RefreshSelectShow(true);
    }

    #endregion

    #region 对象池
    
    private Stack<TreeParentItemCell> _parentItemPool = new Stack<TreeParentItemCell>();
    
    private TreeParentItemCell GetParentItemInPool(Vector2 pos)
    {
        TreeParentItemCell itemCell;
        if (_parentItemPool.Count <= 0)
        {
            itemCell = Instantiate(parentItem);
            itemCell.name = $"TreeParentItemCell_{_usingParentItemList.Count}";
            itemCell.GetComponent<Button>().onClick.AddListener(() => OnClickParentEvent(itemCell));
            itemCell.rectTransform = itemCell.gameObject.GetComponent<RectTransform>();
        }
        else
            itemCell = _parentItemPool.Pop();
        
        itemCell.transform.SetParent(_thisScrollRect.content);
        itemCell.gameObject.SetActive(true);
        itemCell.rectTransform.anchoredPosition = pos;
        _usingParentItemList.Add(itemCell);
        return itemCell;
    }

    private void SetParentItemBackPool(TreeParentItemCell itemCell)
    {
        itemCell.gameObject.SetActive(false);
        _parentItemPool.Push(itemCell);
    }
    
    private Stack<TreeChildItemCell> _childItemPool = new Stack<TreeChildItemCell>();
    
    private TreeChildItemCell GetChildItemInPool(Vector2 pos)
    {
        TreeChildItemCell itemCell;
        if (_childItemPool.Count <= 0)
        {
            itemCell = Instantiate(childItem);
            itemCell.name = $"TreeChildItemCell_{_usingChildItemList.Count}";
            itemCell.GetComponent<Button>().onClick.AddListener(() => OnClickChildEvent(itemCell));
            itemCell.rectTransform = itemCell.gameObject.GetComponent<RectTransform>();
        }
        else
            itemCell = _childItemPool.Pop();
        
        itemCell.transform.SetParent(_thisScrollRect.content);
        itemCell.gameObject.SetActive(true);
        itemCell.rectTransform.anchoredPosition = pos;
        _usingChildItemList.Add(itemCell);
        return itemCell;
    }

    private void SetChildItemBackPool(TreeChildItemCell itemCell)
    {
        itemCell.gameObject.SetActive(false);
        _childItemPool.Push(itemCell);
    }

    #endregion
    
}

public class TreeParentData
{
    public string NameStr;
    public Func<string> GetNameFunc;
    public List<object> ChildDataList;
}

public class TreeBaseItem : MonoBehaviour
{
    [HideInInspector] public int parentCellId;
    [HideInInspector] public RectTransform rectTransform;
    public virtual void SetData(object data) {}
    public virtual void OnRefreshItem() { }
}

public class TreeParentItemCell : TreeBaseItem
{
    public GameObject nromalGroup;
    public GameObject selectGroup;
    public RectTransform arrowRect;
    public Text nameTxt1;
    public Text nameTxt2;
    // public ScrollRect scrollRect;
    [HideInInspector] public TreeParentData Data;

    private float _arrowRotationZ;

    protected virtual void Awake()
    {
        if (arrowRect) _arrowRotationZ = arrowRect.eulerAngles.z;
    }

    public virtual void SetData(TreeParentData data)
    {
        Data = data;
    }

    public void RefreshExpandShow(bool isExpand)
    {
        nromalGroup?.SetActive(!isExpand);
        selectGroup?.SetActive(isExpand);
        if (arrowRect is null) return;
        arrowRect.rotation = Quaternion.Euler(0, 0, isExpand ? _arrowRotationZ - 180f : _arrowRotationZ);
    }
}

public class TreeChildItemCell : TreeBaseItem
{
    [HideInInspector] public int childCellId;
    [HideInInspector] public object Data;
    public override void SetData(object data)
    {
        base.SetData(data);
        Data = data;
    }

    public virtual void RefreshSelectShow(bool isSelect)
    {
        
    }
}

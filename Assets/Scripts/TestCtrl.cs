using System;
using System.Collections.Generic;
using UnityEngine;

public class TestCtrl : MonoBehaviour
{
        public TreeListView treeListView;
        public TreeListView treeListView2;

        private void Start()
        {
                treeListView.ShowTreeList(GetTreeDataList());
                treeListView2.ShowTreeList(GetTreeDataList());
        }

        private List<TreeParentData> GetTreeDataList()
        {
                var dataList = new List<TreeParentData>();
                for (int i = 0; i < 5; i++)
                {
                        var data = new TreeParentData { NameStr = $"parent => {i}" };
                        data.ChildDataList = new List<object>();
                        for (int j = 0; j < (5 - i) * 3; j++)
                        {
                                data.ChildDataList.Add($"child => {j}");
                        }
                        dataList.Add(data);
                }

                return dataList;
        }
}
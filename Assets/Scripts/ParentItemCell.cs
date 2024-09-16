using UnityEngine;

public class ParentItemCell : TreeParentItemCell
{
        public override void SetData(TreeParentData data)
        {
                base.SetData(data);
                nameTxt1.text = data.NameStr;
                nameTxt2.text = data.NameStr;
        }
}
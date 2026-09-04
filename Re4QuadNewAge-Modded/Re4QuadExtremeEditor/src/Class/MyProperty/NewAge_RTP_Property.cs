using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using Re4QuadExtremeEditor.src.Class.Enums;
using Re4QuadExtremeEditor.src.Class.Interfaces;
using Re4QuadExtremeEditor.src.Class.ObjMethods;
using Re4QuadExtremeEditor.src.Class.MyProperty.CustomAttribute;
using Re4QuadExtremeEditor.src.Class.MyProperty.CustomTypeConverter;
using Re4QuadExtremeEditor.src.Class.MyProperty.CustomUITypeEditor;

namespace Re4QuadExtremeEditor.src.Class.MyProperty
{
    [DefaultProperty(nameof(InternalLineID))]
    public class NewAge_RTP_Property : GenericProperty, IInternalID
    {
        public override Type GetClassType()
        {
            return typeof(NewAge_RTP_Property);
        }

        private const GroupType groupType = GroupType.RTP;

        private ushort InternalID = ushort.MaxValue;

        private NewAge_RTP_Methods Methods = null;
        private UpdateMethods updateMethods = null;

        public ushort GetInternalID()
        {
            return InternalID;
        }

        public GroupType GetGroupType()
        {
            return groupType;
        }

        public NewAge_RTP_Property(NewAge_RTP_Property prop, bool ForMultiSelection = false)
        {
            NewAge_RTP_PropertyConstructor(prop.InternalID, prop.updateMethods, prop.Methods, ForMultiSelection);
        }

        public NewAge_RTP_Property(ushort InternalID, UpdateMethods updateMethods, NewAge_RTP_Methods Methods, bool ForMultiSelection = false) : base()
        {
            NewAge_RTP_PropertyConstructor(InternalID, updateMethods, Methods, ForMultiSelection);
        }

        private void NewAge_RTP_PropertyConstructor(ushort InternalID, UpdateMethods updateMethods, NewAge_RTP_Methods Methods, bool ForMultiSelection = false)
        {
            this.InternalID = InternalID;
            this.updateMethods = updateMethods;
            this.Methods = Methods;

            if (!ForMultiSelection)
            {
                SetThis(this);
            }
        }

        #region Category Ids
        private const int CategoryID0_InternalLineID = 0;
        private const int CategoryID1_Node = 1;
        private const int CategoryID2_Links = 2;
        #endregion

        [CustomCategory(aLang.NewAge_InternalLineIDCategory)]
        [CustomDisplayName(aLang.NewAge_InternalLineIDDisplayName)]
        [CustomDescription(aLang.NewAge_InternalLineIDDescription)]
        [DefaultValue(null)]
        [ReadOnly(true)]
        [Browsable(true)]
        [DynamicTypeDescriptor.Id(1, CategoryID0_InternalLineID)]
        public string InternalLineID { get => GetInternalID().ToString(); }

        #region node position

        [CustomCategory(aLang.NewAge_RTP_Category)]
        [CustomDisplayName(aLang.RTP_PosX_DisplayName)]
        [CustomDescription(aLang.RTP_Pos_Description)]
        [TypeConverter(typeof(CAM_FloatTypeConverter))]
        [DefaultValue(null)]
        [ReadOnly(false)]
        [Browsable(true)]
        [AllowInMultiSelect()]
        [DynamicTypeDescriptor.Id(0x100, CategoryID1_Node)]
        public float RTP_PosX
        {
            get => Methods.ReturnPosX(InternalID);
            set { Methods.SetPosX(InternalID, value); }
        }

        [CustomCategory(aLang.NewAge_RTP_Category)]
        [CustomDisplayName(aLang.RTP_PosY_DisplayName)]
        [CustomDescription(aLang.RTP_Pos_Description)]
        [TypeConverter(typeof(CAM_FloatTypeConverter))]
        [DefaultValue(null)]
        [ReadOnly(false)]
        [Browsable(true)]
        [AllowInMultiSelect()]
        [DynamicTypeDescriptor.Id(0x200, CategoryID1_Node)]
        public float RTP_PosY
        {
            get => Methods.ReturnPosY(InternalID);
            set { Methods.SetPosY(InternalID, value); }
        }

        [CustomCategory(aLang.NewAge_RTP_Category)]
        [CustomDisplayName(aLang.RTP_PosZ_DisplayName)]
        [CustomDescription(aLang.RTP_Pos_Description)]
        [TypeConverter(typeof(CAM_FloatTypeConverter))]
        [DefaultValue(null)]
        [ReadOnly(false)]
        [Browsable(true)]
        [AllowInMultiSelect()]
        [DynamicTypeDescriptor.Id(0x300, CategoryID1_Node)]
        public float RTP_PosZ
        {
            get => Methods.ReturnPosZ(InternalID);
            set { Methods.SetPosZ(InternalID, value); }
        }

        #endregion

        #region links

        /// <summary>
        /// the waypoint this node is currently linked to (last entry of its
        /// link list), read straight from the loaded file - so boxes show a
        /// real "Node N" even right after opening an existing RTP
        /// </summary>
        private int CurrentLastLink()
        {
            try
            {
                if (Methods != null && Methods.ReturnLinkedIds != null)
                {
                    ushort[] ids = Methods.ReturnLinkedIds(InternalID);
                    if (ids != null && ids.Length > 0)
                    {
                        return ids[ids.Length - 1];
                    }
                }
            }
            catch (Exception)
            {
            }
            return -1;
        }

        [CustomCategory(aLang.NewAge_RTPLinks_Category)]
        [CustomDisplayName(aLang.RTP_DistanceTableIndex_DisplayName)]
        [CustomDescription(aLang.RTP_DistanceTableIndex_Description)]
        [TypeConverter(typeof(CAM_HexDecTypeConverter))]
        [HexNumber()]
        [DefaultValue(null)]
        [ReadOnly(true)]
        [Browsable(true)]
        [DynamicTypeDescriptor.Id(0x400, CategoryID2_Links)]
        public ushort RTP_DistanceTableIndex
        {
            get => Methods.ReturnDistanceTableIndex(InternalID);
            set { Methods.SetDistanceTableIndex(InternalID, value); }
        }

        [CustomCategory(aLang.NewAge_RTPLinks_Category)]
        [CustomDisplayName(aLang.RTP_ConnectionCount_DisplayName)]
        [CustomDescription(aLang.RTP_ConnectionCount_Description)]
        [TypeConverter(typeof(CAM_HexDecTypeConverter))]
        [HexNumber()]
        [DefaultValue(null)]
        [ReadOnly(true)]
        [Browsable(true)]
        [DynamicTypeDescriptor.Id(0x500, CategoryID2_Links)]
        public ushort RTP_ConnectionCount
        {
            get => Methods.ReturnConnectionCount(InternalID);
            set { Methods.SetConnectionCount(InternalID, value); }
        }

        [CustomCategory(aLang.NewAge_RTPLinks_Category)]
        [CustomDisplayName(aLang.RTP_ConnectTo_DisplayName)]
        [CustomDescription(aLang.RTP_ConnectTo_Description)]
        [TypeConverter(typeof(Rtp_ConnectNodeConverter))]
        [DefaultValue(null)]
        [ReadOnly(false)]
        [Browsable(true)]
        [DynamicTypeDescriptor.Id(0x600, CategoryID2_Links)]
        public int RTP_ConnectTo
        {
            get => CurrentLastLink();
            set
            {
                if (value >= 0)
                {
                    Methods.ConnectTo(InternalID, (ushort)value);
                }
            }
        }

        [CustomCategory(aLang.NewAge_RTPLinks_Category)]
        [CustomDisplayName(aLang.RTP_DisconnectFrom_DisplayName)]
        [CustomDescription(aLang.RTP_DisconnectFrom_Description)]
        [TypeConverter(typeof(Rtp_DisconnectNodeConverter))]
        [DefaultValue(null)]
        [ReadOnly(false)]
        [Browsable(true)]
        [DynamicTypeDescriptor.Id(0x700, CategoryID2_Links)]
        public int RTP_DisconnectFrom
        {
            // IMPORTANT: must return -1 (empty) so PropertyGrid always sees a change
            // when user picks a node from the dropdown. Previous version returned
            // CurrentLastLink() which equals the just-linked node (e.g. 5), so
            // picking "Node 5" to disconnect was seen as no-change and setter
            // was never called -> "فك الربط ما انشال" bug.
            get => -1;
            set
            {
                if (value >= 0)
                {
                    Methods.DisconnectFrom(InternalID, (ushort)value);
                }
            }
        }

        [CustomCategory(aLang.NewAge_RTPLinks_Category)]
        [CustomDisplayName(aLang.RTP_LinksSummary_DisplayName)]
        [CustomDescription(aLang.RTP_LinksSummary_Description)]
        [DefaultValue(null)]
        [ReadOnly(true)]
        [Browsable(true)]
        [DynamicTypeDescriptor.Id(0x800, CategoryID2_Links)]
        public string RTP_LinksSummary
        {
            get => Methods.ReturnLinksSummary(InternalID);
            set { }
        }

        #endregion
    }
}

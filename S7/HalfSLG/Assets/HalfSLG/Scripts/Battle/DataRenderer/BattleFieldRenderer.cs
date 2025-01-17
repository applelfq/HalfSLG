﻿//战场显示器
//同时只有一个战场会被显示

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ELGame
{
    public class BattleFieldRenderer
        : MonoBehaviourSingleton<BattleFieldRenderer>,
          IVisualRenderer<BattleField, BattleFieldRenderer>
    {
        //当前显示的战斗信息
        public BattleField battleField; //战场数据
        public Camera battleCamera;     //渲染战斗的相机

        //格子的模型，用来clone格子拼成地图
        [SerializeField] private GridUnitRenderer gridUnitModel;    
        [SerializeField] private Transform gridUnitsRoot;           

        //战斗单位的模型
        [SerializeField] private BattleUnitRenderer battleUnitModel;
        [SerializeField] private Transform battleUnitsRoot;

        //用来管理创建出来的对象
        private List<GridUnitRenderer> gridRenderersPool = new List<GridUnitRenderer>();            //格子
        private List<BattleUnitRenderer> battleUnitRenderersPool = new List<BattleUnitRenderer>();  //战斗单位

        //Helper:将战场显示器的部分功能分出去写
        private BattleFieldManualOperationHelper manualOperationHelper;     //手动操作的Helper
        
        //初始化
        public void Init(System.Action initedCallback)
        {
            if (gridUnitModel == null
                || gridUnitsRoot == null
                || battleUnitModel == null
                || battleUnitsRoot == null)
            {
                UtilityHelper.LogError("Init battle field renderer failed!");
                return;
            }

            //初始化Helper
            manualOperationHelper = new BattleFieldManualOperationHelper(this);

            UtilityHelper.Log("Init battle field renderer.");

            //创建一定数量的格子和战斗单位渲染器，留作后面使用
            InitGridUnitRenderer(100);
            InitBattleUnitRenderer(10);

            UtilityHelper.Log("Battle field renderer inited.");

            //战场显示器初始化完成，通知回调
            if (initedCallback != null)
            {
                initedCallback();
            }
        }

        private void InitGridUnitRenderer(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                CreateGridUnitRenderer();
            }
        }

        private void InitBattleUnitRenderer(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                CreateBattleUnitRenderer();
            }
        }

        //刷新格子
        private void RefreshBattleMapGrids()
        {
            if (battleField == null)
            {
                UtilityHelper.LogError("Prepare battle map failed. No battle data.");
                return;
            }

            for (int r = 0; r < battleField.battleMap.mapHeight; ++r)
            {
                for (int c = 0; c < battleField.battleMap.mapWidth; ++c)
                {
                    GridUnit gridUnitData = battleField.battleMap.mapGrids[c, r];
                    if (gridUnitData != null)
                    {
                        //创建一个用于显示的格子对象
                        GridUnitRenderer gridUnitRenderer = GetUnusedGridUnitRenderer();
                        if (gridUnitRenderer != null)
                            gridUnitData.ConnectRenderer(gridUnitRenderer);
                    }
                }
            }
        }

        //刷新战斗单位
        private void RefreshBattleUnits()
        {
            if (battleField == null)
            {
                UtilityHelper.LogError("Prepare battle units failed. No battle data.");
                return;
            }

            for (int i = 0; i < battleField.teams.Count; ++i)
            {
                BattleTeam team = battleField.teams[i];
                if (team.battleUnits != null)
                {
                    foreach (var battleUnitData in team.battleUnits)
                    {
                        BattleUnitRenderer battleUnitRenderer = GetUnusedBattleUnitRenderer();
                        battleUnitRenderer.teamColor = i == 0 ? TeamColor.Blue : TeamColor.Red;
                        if (battleUnitRenderer != null)
                            battleUnitData.ConnectRenderer(battleUnitRenderer);
                    }
                }
            }
        }
        
        //创建格子
        private GridUnitRenderer CreateGridUnitRenderer()
        {
            var clone = Instantiate<GridUnitRenderer>(gridUnitModel);
            clone.transform.SetParent(gridUnitsRoot);
            clone.transform.SetUnused(false, EGameConstL.STR_Grid);
            clone.Init();
            gridRenderersPool.Add(clone);
            return clone;
        }

        //创建战斗单位
        private BattleUnitRenderer CreateBattleUnitRenderer()
        {
            var clone = Instantiate<BattleUnitRenderer>(battleUnitModel);
            clone.transform.SetParent(battleUnitsRoot);
            clone.transform.SetUnused(false, EGameConstL.STR_BattleUnit);
            clone.Init();
            battleUnitRenderersPool.Add(clone);
            return clone;
        }

        //获取没有使用的格子渲染器
        private GridUnitRenderer GetUnusedGridUnitRenderer()
        {
            for (int i = 0; i < gridRenderersPool.Count; ++i)
            {
                if (!gridRenderersPool[i].gameObject.activeSelf)
                    return gridRenderersPool[i];
            }
            return CreateGridUnitRenderer();
        }

        //获取没有使用的战斗单位渲染器
        private BattleUnitRenderer GetUnusedBattleUnitRenderer()
        {
            for (int i = 0; i < battleUnitRenderersPool.Count; ++i)
            {
                if (!battleUnitRenderersPool[i].gameObject.activeSelf)
                    return battleUnitRenderersPool[i];
            }
            return CreateBattleUnitRenderer();
        }

        //战场连接
        public void OnConnect(BattleField field)
        {
            battleField = field;
            //加载战场
            RefreshBattleMapGrids();
            //加载战斗单位
            RefreshBattleUnits();
        }

        //战场取消连接
        public void OnDisconnect()
        {
            if (battleField != null)
            {
                battleField = null;
            }
        }

        private void Update()
        {
            if (battleField == null)
                return;
            
            UpdateBattleFieldTouched();
        }

        //获取战场点击的情况
        private void UpdateBattleFieldTouched()
        {
            //如果点击了鼠标左键
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    //点中了UI
                    UtilityHelper.Log("点中了UI");
                    return;
                }
                ClickedBattleField(Input.mousePosition);
            }
            else if (Input.GetMouseButtonDown(1))
            {
                //右键点击为取消
                manualOperationHelper.ClickedCancel();
            }
        }

        //通过屏幕点击了战场
        private void ClickedBattleField(Vector3 screenPosition)
        {
            //计算点击位置
            Vector3 clickedWorldPos = battleCamera.ScreenToWorldPoint(screenPosition);
            clickedWorldPos.z = 0;
            //判断是否有格子被点中？
            GridUnitRenderer clicked = GetGridClicked(clickedWorldPos);
            if (clicked != null && clicked.gridUnit != null)
            {
                //发生点击喽~
                OnBattleUnitAndGridTouched(clicked.gridUnit, clicked.gridUnit.battleUnit);
            }
            else
            {
                //点到了地图外，关闭所有弹出层界面
                UIViewManager.Instance.HideViews(UIViewLayer.Popup);
            }
        }

        //根据点击位置获取点中的格子
        private GridUnitRenderer GetGridClicked(Vector3 clickedWorldPos)
        {
            //转换空间到格子组织节点(GridUnits)的空间
            clickedWorldPos = gridUnitsRoot.transform.InverseTransformPoint(clickedWorldPos);
            //初步判定所在行列
            int row = Mathf.FloorToInt((clickedWorldPos.y - EGameConstL.Map_GridOffsetY * 0.5f) / -EGameConstL.Map_GridOffsetY);
            int column = Mathf.FloorToInt((clickedWorldPos.x + EGameConstL.Map_GridWidth * 0.5f - ((row & 1) == (EGameConstL.Map_FirstRowOffset ? 1 : 0) ? 0f : (EGameConstL.Map_GridWidth * 0.5f))) / EGameConstL.Map_GridWidth);

            int testRow = 0;
            int testColumn = 0;
            //二次判定，判定周围格子
            GridUnitRenderer clickedGrid = null;
            float minDis = Mathf.Infinity;
            for (int r = -1; r <= 1; ++r)
            {
                for (int c = -1; c <= 1; ++c)
                {
                    testRow = row + r;
                    testColumn = column + c;
                    if (testRow < 0 || testRow >= battleField.battleMap.mapHeight
                        || testColumn < 0 || testColumn >= battleField.battleMap.mapWidth)
                    {
                        continue;
                    }
                    float distance = UtilityHelper.CalcDistanceInXYAxis(clickedWorldPos, battleField.battleMap.mapGrids[testColumn, testRow].localPosition);
                    if (distance < minDis && distance < EGameConstL.Map_HexRadius)
                    {
                        minDis = distance;
                        clickedGrid = battleField.battleMap.mapGrids[testColumn, testRow].gridUnitRenderer;
                    }
                }
            }
            return clickedGrid;
        }

        //点击了地块、战斗单位
        private void OnBattleUnitAndGridTouched(GridUnit gridTouched, BattleUnit battleUnitTouched)
        {
            //通知helper处理点击反馈逻辑
            manualOperationHelper.OnBattleUnitAndGridTouched(gridTouched, battleUnitTouched);
        }

        //设置手动操作的英雄
        public void SetManualBattleUnit(BattleUnitRenderer operatingBattleUnit)
        {
            manualOperationHelper.ManualOperatingBattleUnitRenderer = operatingBattleUnit;
        }

        //某个战斗单位点击了移动
        public void BattleUnitMove(BattleUnit battleUnit)
        {
            manualOperationHelper.BattleUnitMove(battleUnit);
        }

        //某个战斗单位点击了待命
        public void BattleUnitStay(BattleUnit battleUnit)
        {
            manualOperationHelper.BattleUnitStay(battleUnit);
        }

        //某个战斗单位点击了使用技能
        public void BattleUnitUseSkill(BattleUnit battleUnit, SO_BattleSkill skill)
        {
            manualOperationHelper.BattleUnitUseSkill(battleUnit, skill);
        }

        //设置某个圆形区域的显示状态
        public void SetCircularRangeRenderStateActive(bool active, GridRenderType gridRenderType, int centerRow = -1, int centerColumn = -1, int radius = -1)
        {
            manualOperationHelper.SetCircularRangeRenderStateActive(active, gridRenderType, centerRow, centerColumn, radius);
        }

        //设置路径显示状态
        public void SetGridsRenderStateActive(bool active, GridUnit[] gridPath = null)
        {
            manualOperationHelper.SetGridsRenderStateActive(active, gridPath);
        }

        //播放战场动作
        private IEnumerator PlayBattleByCoroutine(System.Action callback)
        {
            if (battleField == null
                || battleField.msgAction.battleActions == null
                || battleField.msgAction.battleActions.Count == 0)
            {
                UtilityHelper.LogError(string.Format("Play battle action failed. -> {0}", battleField.battleID));
                yield break;
            }

            UtilityHelper.Log("Play battle actions");

            //遍历所有战斗动作
            var msgAction = battleField.msgAction;
            while (battleField.currentIndex < msgAction.battleActions.Count)
            {
                if (msgAction.battleActions[battleField.currentIndex] == null)
                {
                    UtilityHelper.LogError(string.Format("Play action error. Action is none or type is none, index = {0}", battleField.currentIndex));
                    continue;
                }

                BattleHeroAction heroAction = null;
                //一个英雄动作
                if (msgAction.battleActions[battleField.currentIndex] is BattleHeroAction)
                {
                    heroAction = (BattleHeroAction)msgAction.battleActions[battleField.currentIndex];

                    //有对应的战斗单位，且这个战斗单位已经连接了战斗单位渲染器
                    if (heroAction.actionUnit != null && heroAction.actionUnit.battleUnitRenderer != null)
                    {
                        yield return heroAction.actionUnit.battleUnitRenderer.RunHeroAction(heroAction);
                    }
                }
                ++battleField.currentIndex;
            }

            UtilityHelper.Log("Play Msg Action fin");

            if (callback != null)
                callback();
        }

        //播放战斗(异步的方式)
        public void PlayBattle(System.Action callback)
        {
            StartCoroutine(PlayBattleByCoroutine(callback));
        }

        //战斗结束
        public void BattleEnd()
        {
            var viewMain = UIViewManager.Instance.GetViewByName<UIViewMain>(UIViewName.Main);
            if (viewMain != null)
                viewMain.ShowBattleEnd();
        }
    }
}
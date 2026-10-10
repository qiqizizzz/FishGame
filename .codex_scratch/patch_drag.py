# -*- coding: utf-8 -*-
import io, os

ROOT = r"D:\CCodes\unity\FishGame\Assets\Scripts\UI\Shop"

def load(name):
    p = os.path.join(ROOT, name)
    raw = io.open(p, "r", encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    return p, raw.replace("\r\n", "\n"), crlf

def save(p, text, crlf):
    out = text.replace("\n", "\r\n") if crlf else text
    io.open(p, "w", encoding="utf-8", newline="").write(out)

def patch(name, pairs):
    p, text, crlf = load(name)
    miss = 0
    for old, new in pairs:
        n = text.count(old)
        if n != 1:
            print("  MISS(%d) %s :: %s" % (n, name, old.split(chr(10))[0][:70]))
            miss += 1
            continue
        text = text.replace(old, new)
    if miss:
        print("SKIP WRITE %s (miss=%d)" % (name, miss))
        return False
    save(p, text, crlf)
    print("OK %s crlf=%s" % (name, crlf))
    return True

ok = True

# ---------------- UIShopPlateView.cs ----------------
ok &= patch("UIShopPlateView.cs", [
(
"""            m_isDragging = false;
            applyClothCellsVisible();""",
"""            m_isDragging = false;
            m_wasDragged = false;
            applyClothCellsVisible();"""
),
(
"""        // 松手即落点，四舍五入到最近的格，行主序与铺块时的算法完全一致""",
"""        // 松手即落点，四舍五入到最近的格，行主序与铺块时的算法完全一致
        // 松手在网格外时收得到 endDrag 却收不到 click，拖过的标记只能在这里清，否则下一次真点击会被吞掉"""
),
])

# ---------------- UIShopItemView.cs ----------------
ok &= patch("UIShopItemView.cs", [
(
"""            // 中途宿主可能已经重铺过本视图，落点定死之后再把手上的格子块收回图标态
            m_isDragging = false;
            applyDragVisual();""",
"""            // 中途宿主可能已经重铺过本视图，落点定死之后再把手上的格子块收回图标态
            // 松手在网格外时收得到 endDrag 却收不到 click，拖过的标记只能在这里清，否则下一次真点击会被吞掉
            m_isDragging = false;
            m_wasDragged = false;
            applyDragVisual();"""
),
(
"""            m_isDragging = false;
            applyDragVisual();
        }

        // ==================== Public Function ====================""",
"""            m_isDragging = false;
            m_wasDragged = false;
            applyDragVisual();
        }

        // ==================== Public Function ===================="""
),
])

# ---------------- UIShopSlotItem.cs ----------------
ok &= patch("UIShopSlotItem.cs", [
(
"""        // 面板关掉时走不到 OnEndDrag，这里补一次收摊，免得幽灵格留在场上
        private void OnDisable()
        {
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
        }""",
"""        // 面板关掉时走不到 OnEndDrag，这里补一次收摊，免得幽灵格留在场上
        private void OnDisable()
        {
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_wasDragged = false;
        }"""
),
(
"""        // 松手按幽灵吸附的那格摆放，指到网格外就什么都不做，东西留在架上
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            Vector2 screenPoint = eventData.position;
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;""",
"""        // 松手按幽灵吸附的那格摆放，指到网格外就什么都不做，东西留在架上
        // 落在网格外时收得到 endDrag 却收不到 click，拖过的标记只能在这里清，否则下一次真点击会被吞掉
        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_host == null || m_item == null)
                return;

            Vector2 screenPoint = eventData.position;
            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_wasDragged = false;"""
),
])

print("ALL_OK" if ok else "HAS_MISS")

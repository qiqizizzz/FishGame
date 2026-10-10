# -*- coding: utf-8 -*-
import io, os

P = r"D:\CCodes\unity\FishGame\Assets\Scripts\UI\Shop\UIShopGoodsItem.cs"
raw = io.open(P, "r", encoding="utf-8", newline="").read()
crlf = "\r\n" in raw
t = raw.replace("\r\n", "\n")

pairs = [
(
"* 点一下只报详情不扣钱，拖动之后同帧紧跟的那次点击按帧号挡掉，免得拖完又弹出小窗",
"* 点一下只报详情不扣钱，真拖过之后紧跟的那次点击按拖过标记挡掉，免得拖完又弹出小窗"
),
(
"""        // 松手那一帧 EventSystems 紧跟着一发 pointerClick，同帧的点击只是拖完的余波，不能当「按一下看详情」
        private int m_dragFrame;""",
"""        // 松手那一发点击只是拖完的余波，不能当「按一下看详情」；拖过与否只能自己记
        private bool m_wasDragged;"""
),
(
"""            m_entry = entry;
            m_host = host;
            m_ghostColor = goodsColor;""",
"""            m_entry = entry;
            m_host = host;
            m_ghostColor = goodsColor;
            m_wasDragged = false;"""
),
(
"""            m_dragFrame = Time.frameCount;
            m_host.BeginGoodsDrag(m_entry);""",
"""            m_wasDragged = true;
            m_host.BeginGoodsDrag(m_entry);"""
),
(
"""            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_dragInstance = null;""",
"""            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_dragInstance = null;
            // 落在货架之外就只收得到 endDrag，收不到 click，标记只能在这里清，否则下一次真点击会被吞掉
            m_wasDragged = false;"""
),
(
"""            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_dragInstance = null;
        }""",
"""            UIShopShapeGhost.Recycle(m_ghost);
            m_ghost = null;
            m_dragInstance = null;
            m_wasDragged = false;
        }"""
),
(
"""        private void handleClicked()
        {
            if (m_dragFrame == Time.frameCount)
                return;

            if (m_host == null || m_entry == null)
                return;""",
"""        private void handleClicked()
        {
            if (m_wasDragged)
            {
                m_wasDragged = false;
                return;
            }

            if (m_host == null || m_entry == null)
                return;"""
),
]

miss = 0
for old, new in pairs:
    n = t.count(old)
    if n != 1:
        print("MISS(%d) :: %s" % (n, old.split(chr(10))[0][:70]))
        miss += 1
        continue
    # OnDisable 那处与 OnEndDrag 那处共用前两行，先做过 OnEndDrag 的替换后此处应恰好命中一次
    t = t.replace(old, new)

if miss:
    print("SKIP WRITE, miss=%d" % miss)
else:
    if not t.endswith("\n"):
        t += "\n"
    out = t.replace("\n", "\r\n") if crlf else t
    io.open(P, "w", encoding="utf-8", newline="").write(out)
    print("OK crlf=%s endsWithNewline=%s" % (crlf, out.endswith("\n")))

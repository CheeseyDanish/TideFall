// TideWall 3D — Player: opening and driving menus while walking, and the body's look (size, colour, blinking).
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ menus
        /// <summary>Open a menu in this player's view. On a client, pause/settings menus stay local.</summary>
        public void OpenMenu(Menu m)
        {
            if (Kind == PlayerKind.LocalProxy) LocalMenu = m; else OpenedMenu = m;
            Sfx.Play("ui");
        }

        public void CloseMenu()
        {
            if (Kind == PlayerKind.LocalProxy) LocalMenu = null; else OpenedMenu = null;
        }

        void UpdateMenu(float dt)
        {
            var menu = ShownMenu;
            if (menu == null || Rebinder.Busy) return;
            var nav = NavInput();
            menuRepeat -= dt;
            int dir = nav.y > .5f ? -1 : nav.y < -.5f ? 1 : 0;
            int side = nav.x > .5f ? 1 : nav.x < -.5f ? -1 : 0;
            bool freshV = Mathf.Abs(lastNav.y) <= .5f, freshH = Mathf.Abs(lastNav.x) <= .5f;
            if (dir != 0 && (freshV || menuRepeat <= 0))
            {
                if (menu.Columns > 0) menu.MoveGrid(0, dir); else menu.Move(dir);
                menuRepeat = freshV ? .35f : .1f;
                Sfx.Play("ui", .5f);
            }
            if (side != 0 && (freshH || menuRepeat <= 0))
            {
                var it = menu.Current;
                if (menu.Columns > 0) { menu.MoveGrid(side, 0); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .1f; }
                else if (menu.Horizontal) { menu.Move(side); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .1f; }
                else if (it?.OnAdjust != null) { it.OnAdjust(side); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .08f; }
                else if (menu.OnSide != null) { menu.OnSide(side); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .12f; Refresh(menu); return; }
            }
            lastNav = nav;
            if (menu.OnTab != null && (Pressed(Btn.Swap) || Pressed(Btn.Reload)))
            {
                var next = menu.OnTab(Pressed(Btn.Swap) ? 1 : -1);
                if (next != null) { ReplaceMenu(next); Sfx.Play("ui"); }
                return;
            }
            if (menu.Quick != null && Pressed(Btn.Reload))
            {
                menu.Quick();
                Refresh(menu);
                return;
            }
            if (Pressed(Btn.Back))
            {
                var parent = menu.Parent?.Invoke();
                if (parent != null) ReplaceMenu(parent); else CloseMenu();
                return;
            }
            if (Pressed(Btn.Interact))
            {
                var item = menu.Current;
                if (item == null) return;
                var why = item.Blocked?.Invoke();
                if (why != null) { Sfx.Play("deny"); Toast(why, new Color(1, .6f, .6f)); return; }
                item.OnPick?.Invoke();
                var next = item.Open?.Invoke();
                if (ShownMenu != menu) return;              // the pick closed or replaced the menu itself
                if (next != null) { ReplaceMenu(next); return; }
                Refresh(menu);
            }
        }

        /// <summary>Rebuild the open menu after a change (prices, labels), keeping the selected row.</summary>
        void Refresh(Menu menu)
        {
            if (ShownMenu != menu) return;
            var rebuilt = menu.Rebuild?.Invoke();
            if (rebuilt != null) { rebuilt.Selected = Mathf.Clamp(menu.Selected, 0, rebuilt.Items.Count - 1); ReplaceMenu(rebuilt); }
        }

        void ReplaceMenu(Menu m)
        {
            if (Kind == PlayerKind.LocalProxy) LocalMenu = m; else OpenedMenu = m;
        }

        // ------------------------------------------------------------------ look
        void UpdateVisual(float dt)
        {
            float scale = Statuses.Has(StatusType.Giant) ? 1.45f : Statuses.Has(StatusType.Tiny) ? .6f : 1;
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * scale, dt * 6);
            if (Statuses.Has(StatusType.Rainbow)) ModelFactory.Recolor(model, "coat", Color.HSVToRGB(Time.time * .5f % 1, .8f, 1));
            else if (Statuses.Has(StatusType.Rage)) ModelFactory.Recolor(model, "coat", Color.Lerp(Color, Color.red, .5f + .5f * Mathf.Sin(Time.time * 12)));
            if (Statuses.Has(StatusType.Bubbles) && Random.value < dt * 10 && Simulated) Fx.Bubbles(transform.position + Vector3.up * 1.8f, 1);
            if (Statuses.Has(StatusType.Speed) && Random.value < dt * 12 && Simulated) Fx.Smoke(transform.position, 1, .2f, new Color(1, .9f, .4f, .4f));
            bool blink = Statuses.Has(StatusType.Invincible) && Mathf.Sin(Time.time * 30) > 0;
            foreach (var r in model.Renderers) r.enabled = !blink;
            AnimateBody(dt);
        }
    }
}

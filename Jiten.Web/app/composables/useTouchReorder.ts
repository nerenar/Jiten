import type { Ref } from 'vue';

interface TouchReorderOptions {
  containerRef: Ref<HTMLElement | null>;
  onReorder: (fromIndex: number, toIndex: number) => void;
}

export function useTouchReorder(options: TouchReorderOptions) {
  const dragIndex = ref<number | null>(null);
  const dropIndex = ref<number | null>(null);

  let activePointerId: number | null = null;
  let ghost: HTMLElement | null = null;
  let offsetY = 0;

  function getDropTarget(clientY: number): number {
    const container = options.containerRef.value;
    if (!container) return 0;
    const children = container.children;
    for (let i = 0; i < children.length; i++) {
      const rect = children[i].getBoundingClientRect();
      if (clientY < rect.top + rect.height / 2) return i;
    }
    return children.length - 1;
  }

  function createGhost(sourceEl: HTMLElement, clientY: number) {
    const rect = sourceEl.getBoundingClientRect();
    offsetY = clientY - rect.top;

    const clone = sourceEl.cloneNode(true) as HTMLElement;
    clone.style.position = 'fixed';
    clone.style.left = `${rect.left}px`;
    clone.style.top = `${clientY - offsetY}px`;
    clone.style.width = `${rect.width}px`;
    clone.style.zIndex = '9999';
    clone.style.pointerEvents = 'none';
    clone.style.opacity = '0.85';
    clone.style.boxShadow = '0 8px 24px rgba(0,0,0,0.18)';
    clone.style.transition = 'none';
    document.body.appendChild(clone);
    ghost = clone;
  }

  function cancelDrag() {
    document.removeEventListener('pointermove', onMove);
    document.removeEventListener('pointerup', onUp);
    document.removeEventListener('pointercancel', onUp);
    window.removeEventListener('blur', cancelDrag);

    if (ghost) {
      ghost.remove();
      ghost = null;
    }

    activePointerId = null;
    dragIndex.value = null;
    dropIndex.value = null;
  }

  function onMove(ev: PointerEvent) {
    if (ev.pointerId !== activePointerId) return;
    ev.preventDefault();
    dropIndex.value = getDropTarget(ev.clientY);
    if (ghost) ghost.style.top = `${ev.clientY - offsetY}px`;
  }

  function onUp(ev: PointerEvent) {
    if (ev.pointerId !== activePointerId) return;

    const from = dragIndex.value;
    const to = dropIndex.value;
    cancelDrag();

    if (from !== null && to !== null && from !== to) {
      options.onReorder(from, to);
    }
  }

  function handlePointerDown(e: PointerEvent, index: number) {
    if (e.button !== 0) return;
    e.preventDefault();

    if (activePointerId !== null) cancelDrag();

    activePointerId = e.pointerId;
    dragIndex.value = index;
    dropIndex.value = index;

    const container = options.containerRef.value;
    if (container) {
      const item = container.children[index] as HTMLElement | undefined;
      if (item) createGhost(item, e.clientY);
    }

    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
    document.addEventListener('pointercancel', onUp);
    window.addEventListener('blur', cancelDrag);
  }

  onUnmounted(cancelDrag);

  return {
    dragIndex: readonly(dragIndex),
    dropIndex: readonly(dropIndex),
    handlePointerDown,
  };
}

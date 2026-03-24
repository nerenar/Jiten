import { FsrsRating } from '~/types';
import { useSrsStore } from '~/stores/srsStore';

export interface StudyKeyboardCallbacks {
  onGrade: (rating: FsrsRating) => void;
  onBlacklist: () => void;
  onForget: () => void;
  onMaster: () => void;
  onSuspend: () => void;
  onUndo: () => void;
  onWrapUp: () => void;
}

export function useStudyKeyboard(callbacks: StudyKeyboardCallbacks) {
  const store = useSrsStore();
  const router = useRouter();
  const pressedKey = ref<string | null>(null);
  let pressedTimeout: ReturnType<typeof setTimeout> | null = null;

  function flashKey(key: string) {
    pressedKey.value = key;
    if (pressedTimeout) clearTimeout(pressedTimeout);
    pressedTimeout = setTimeout(() => { pressedKey.value = null; }, 150);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;
    if (e.ctrlKey || e.altKey || e.metaKey) return;
    if (store.isBusy) return;
    const code = e.code;

    if (store.isFlipped && (code === 'Digit1' || code === 'Numpad1')) {
      flashKey('1');
      callbacks.onGrade(FsrsRating.Again);
      return;
    }
    if (store.isFlipped && (code === 'Digit2' || code === 'Numpad2') && store.studySettings.gradingButtons === 4) {
      flashKey('2');
      callbacks.onGrade(FsrsRating.Hard);
      return;
    }
    if (store.isFlipped && (code === 'Digit3' || code === 'Numpad3') && store.studySettings.gradingButtons === 4) {
      flashKey('3');
      callbacks.onGrade(FsrsRating.Good);
      return;
    }
    if (store.isFlipped && (code === 'Digit2' || code === 'Numpad2') && store.studySettings.gradingButtons === 2) {
      flashKey('2');
      callbacks.onGrade(FsrsRating.Good);
      return;
    }
    if (store.isFlipped && (code === 'Digit4' || code === 'Numpad4') && store.studySettings.gradingButtons === 4) {
      flashKey('4');
      callbacks.onGrade(FsrsRating.Easy);
      return;
    }

    switch (e.key) {
      case ' ':
      case 'Enter':
        e.preventDefault();
        if (!store.isFlipped) {
          flashKey('space');
          store.revealCard();
        } else {
          flashKey(store.studySettings.gradingButtons === 2 ? '2' : '3');
          callbacks.onGrade(FsrsRating.Good);
        }
        break;
      case 'b':
      case 'B':
        if (store.currentCard && store.isFlipped) { flashKey('b'); callbacks.onBlacklist(); }
        break;
      case 'f':
      case 'F':
        if (store.currentCard && store.isFlipped) { flashKey('f'); callbacks.onForget(); }
        break;
      case 'm':
      case 'M':
        if (store.currentCard && store.isFlipped) { flashKey('m'); callbacks.onMaster(); }
        break;
      case 's':
      case 'S':
        if (store.currentCard && store.isFlipped) { flashKey('s'); callbacks.onSuspend(); }
        break;
      case 'z':
      case 'Z':
        if (store.canUndo) { flashKey('z'); callbacks.onUndo(); }
        break;
      case 'w':
      case 'W':
      case 'Escape':
        flashKey('w');
        callbacks.onWrapUp();
        break;
    }
  }

  onMounted(() => {
    window.addEventListener('keydown', handleKeydown);
  });

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeydown);
  });

  return { pressedKey: readonly(pressedKey) };
}

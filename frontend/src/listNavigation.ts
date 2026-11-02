import { useCallback, useLayoutEffect, useState } from 'react'
import type { MouseEvent } from 'react'

const initialPosition = { scrollY: 0, lastViewedId: null as string | null }

export function useListNavigation() {
  const [position, setPosition] = useState(initialPosition)
  const clear = useCallback(() => setPosition(initialPosition), [])
  const remember = useCallback((id: string, event: MouseEvent<HTMLAnchorElement>) => {
    // Opening another tab/window must not change this tab's return position.
    if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return
    setPosition({ scrollY: window.scrollY, lastViewedId: id })
  }, [])
  return { ...position, remember, clear }
}

export type ListNavigation = ReturnType<typeof useListNavigation>

export function useListScroll(navigation: ListNavigation, isList: boolean, ready: boolean) {
  useLayoutEffect(() => {
    // Restore once rows are loaded and visible. Placeholders and content hidden
    // by a session check cannot support the saved scroll position.
    if (isList && ready) window.scrollTo({ top: navigation.scrollY, left: 0, behavior: 'instant' })
  }, [isList, ready, navigation.scrollY])
}

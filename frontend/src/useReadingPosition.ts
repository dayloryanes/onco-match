import { useCallback, useLayoutEffect, useRef, useState } from 'react'

type Anchor = { element: Element; top: number }
type Position = { anchors: Anchor[]; overflowAnchor: string }
export type PreserveReadingPosition = (update: () => void) => void

export function useReadingPosition(): PreserveReadingPosition {
  const position = useRef<Position | null>(null)
  const [revision, setRevision] = useState(0)

  const release = useCallback(() => {
    const saved = position.current
    position.current = null
    if (saved) document.documentElement.style.overflowAnchor = saved.overflowAnchor
    return saved
  }, [])

  useLayoutEffect(() => {
    const saved = position.current
    if (!saved) return
    try {
      const anchor = saved.anchors.find(item => item.element.isConnected)
      if (anchor) {
        const shift = anchor.element.getBoundingClientRect().top - anchor.top
        if (Math.abs(shift) > .5) window.scrollBy({ top: shift, left: 0, behavior: 'instant' })
      }
    } finally { release() }
  }, [revision, release])

  useLayoutEffect(() => {
    // Respect input arriving between capture and commit; release on navigation
    // without scrolling the destination page or leaving native anchoring off.
    const cancel = () => { release() }
    const events = ['wheel', 'touchmove', 'pointerdown', 'keydown', 'resize'] as const
    events.forEach(event => window.addEventListener(event, cancel, { passive: true }))
    return () => {
      events.forEach(event => window.removeEventListener(event, cancel))
      release()
    }
  }, [release])

  return useCallback(update => {
    if (!position.current) {
      let element: Element | null = window.scrollY === 0 ? document.documentElement
        : document.elementFromPoint?.(window.innerWidth / 2, window.innerHeight / 2) ?? null
      // A collection's own position stays fixed when children are prepended.
      // If the sample hits the gap between children, anchor visible content.
      while (element?.matches('ol, ul, table, tbody')) {
        let nearest: Element | null = null
        let nearestDistance = Infinity
        for (const child of element.children) {
          const bounds = child.getBoundingClientRect()
          if (bounds.height === 0 || bounds.bottom <= 0 || bounds.top >= window.innerHeight) continue
          const distance = Math.max(bounds.top - window.innerHeight / 2, window.innerHeight / 2 - bounds.bottom, 0)
          if (distance < nearestDistance) { nearest = child; nearestDistance = distance }
        }
        if (!nearest) break
        element = nearest
      }
      const anchors: Anchor[] = []
      while (element) {
        anchors.push({ element, top: element.getBoundingClientRect().top })
        element = element.parentElement
      }
      if (anchors.length) {
        position.current = { anchors, overflowAnchor: document.documentElement.style.overflowAnchor }
        // Avoid applying both native anchoring and our correction to one update.
        document.documentElement.style.overflowAnchor = 'none'
      }
    }
    try {
      update()
      if (position.current) setRevision(value => value + 1)
    } catch (failure) { release(); throw failure }
  }, [release])
}

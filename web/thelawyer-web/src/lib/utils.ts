import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Standard shadcn/ui className merger. clsx handles conditionals + array/object input;
 * tailwind-merge dedupes conflicting Tailwind utility classes (last wins).
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

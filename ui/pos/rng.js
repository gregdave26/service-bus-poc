const maxSeed = 2 ** 32;

/**
 * Mulberry32: a small, fast, deterministic PRNG. Given the same seed it always
 * produces the same sequence of floats in [0, 1), which is what lets receipt
 * generation be reproduced exactly from a stored seed value.
 */
export function createSeededRandom(seed) {
  let state = seed >>> 0;
  return function next() {
    state = (state + 0x6d2b79f5) | 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function generateRandomSeed() {
  return Math.floor(Math.random() * maxSeed);
}

export function pickInt(random, min, max) {
  return min + Math.floor(random() * (max - min + 1));
}

export function pickItem(random, items) {
  return items[pickInt(random, 0, items.length - 1)];
}

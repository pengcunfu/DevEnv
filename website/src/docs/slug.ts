/**
 * 标题锚点生成规则。markdown-it-anchor 与目录 / 搜索共用同一份实现，
 * 保证文章里的 #fragment 与生成的 id 一致。
 */
export function slugify(text: string): string {
  return text
    .replace(/<[^>]+>/g, '')
    .replace(/[`*_~]/g, '')
    .trim()
    .toLowerCase()
    .replace(/\s+/g, '-')
    .replace(/[^\p{L}\p{N}\-_]/gu, '')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
}

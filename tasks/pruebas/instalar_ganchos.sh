#!/bin/bash
# Instala el gancho pre-push de Conquest (.git/hooks no viaja con el repo): la pasada visual (tasks/pruebas/pre-push-visual.sh) y después,
# tal cual, la de Git LFS. Se puede volver a correr: reescribe el gancho entero. Desde la carpeta de Conquest:
#   bash tasks/pruebas/instalar_ganchos.sh
CQ=$(cd "$(dirname "$0")/../.." && pwd)
G=$(git -C "$CQ" rev-parse --git-path hooks)
case "$G" in /*|?:*) ;; *) G="$CQ/$G" ;; esac
mkdir -p "$G"
[ -f "$G/pre-push" ] && ! grep -q "pre-push-visual" "$G/pre-push" && cp "$G/pre-push" "$G/pre-push.antes-visual"
cat > "$G/pre-push" <<'GANCHO'
#!/bin/sh
# (instalado por tasks/pruebas/instalar_ganchos.sh) la pasada visual de Conquest y, después, Git LFS. Git pasa por la entrada la lista de lo
# que se sube: se guarda una vez y se le da a los dos.
REFS=$(mktemp)
cat > "$REFS"
RAIZ=$(git rev-parse --show-toplevel)
bash "$RAIZ/tasks/pruebas/pre-push-visual.sh" "$REFS" || { rm -f "$REFS"; exit 1; }
command -v git-lfs >/dev/null 2>&1 || { printf >&2 "\n%s\n\n" "This repository is configured for Git LFS but 'git-lfs' was not found on your path. If you no longer wish to use Git LFS, remove this hook by deleting the 'pre-push' file in the hooks directory (set by 'core.hookspath'; usually '.git/hooks')."; rm -f "$REFS"; exit 2; }
git lfs pre-push "$@" < "$REFS"
r=$?
rm -f "$REFS"
exit $r
GANCHO
chmod +x "$G/pre-push"
echo "gancho pre-push instalado en $G/pre-push (el de antes, si era otro, en pre-push.antes-visual)"

"""Hace un ticket de batalla de prueba para la batalla de verdad (-batallaReal con -ticket), a partir de un dorado
del motor: mismos campos, con cuatro tropas del motor que Conquest ya tiene en su base de escuadras.

    python Tools/ticket_de_prueba.py <salida.json> [dorado.json] [--campo-abierto]

El dorado es un asalto a una caravana, donde el héroe que cae reaparece. Con --campo-abierto la batalla pasa a ser de
columna contra columna, donde el que cae sale de la batalla.

Bando atacante: un héroe de la IA con hoplitas (18) y una escuadra sin héroe de granjeros (20 de 30).
Bando defensor: un héroe humano con arqueros (18, en línea y sin poder dispersarse) y otro de la IA con milicia de lanceros (15 de 25).
"""
import json
import sys

DORADO = '../BronzeAgeFase0/contratos/golden/MensajeDeServidor__batalla_ticket_de_caravana_con_dos.json'


def escuadra(squad_id, tropa, efectivos, heroe=None, formacion='reposo', permitidas=('reposo', 'linea', 'dispersa', 'cuna', 'muro_escudos', 'testudo')):
    e = {'squadId': squad_id, 'tropaId': tropa, 'efectivosAutorizados': efectivos, 'nivel': 1,
         'formacion': formacion, 'formacionesPermitidas': list(permitidas)}
    if heroe:
        e['heroeId'] = heroe
    return e


def participante(heroe_id, nombre, clase, es_bot, escuadras):
    p = {'heroeId': heroe_id, 'heroe': {'displayName': nombre, 'classDefinitionId': clase, 'nivel': 3}, 'escuadras': escuadras}
    if es_bot:
        p['esBot'] = True
    return p


def main():
    rutas = [a for a in sys.argv[1:] if not a.startswith('--')]
    salida = rutas[0]
    mensaje = json.load(open(rutas[1] if len(rutas) > 1 else DORADO, encoding='utf-8'))
    ticket = mensaje['batalla']['ticket']
    # La versión del catálogo de tropas que importó Conquest (Assets/Resources/Squads/BronzeAge/catalogo.json).
    ticket['reglas']['versionCatalogoTropas'] = json.load(open('Assets/Resources/Squads/BronzeAge/catalogo.json', encoding='utf-8'))['version']
    if '--campo-abierto' in sys.argv:
        ticket['contextoEstrategico'] = {'campoAbierto': {'punto': {'x': '125000', 'y': '345000'}}}
    ticket['atacante'] = {
        'capacidadMaxima': 5,
        'participantes': [participante('heroe-20', 'Bóreas', 'hacha_dos_manos', True, [escuadra('esc-40', 'hoplitas_ciudadanos', 18, 'heroe-20')])],
        'escuadrasSinHeroe': [escuadra('esc-41', 'granjeros', 20)],
    }
    ticket['defensor'] = {
        'faccionId': 'f-1',
        'capacidadMaxima': 5,
        'participantes': [
            participante('heroe-3', 'Aldric', 'arco_lanza', False, [escuadra('esc-9', 'arqueros', 18, 'heroe-3', 'linea', ('reposo', 'linea'))]),
            participante('heroe-4', 'Cora', 'espada_escudo_mediano', True, [escuadra('esc-10', 'milicia_lanceros', 15, 'heroe-4')]),
        ],
    }
    json.dump(mensaje, open(salida, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('ticket en', salida)


if __name__ == '__main__':
    main()
